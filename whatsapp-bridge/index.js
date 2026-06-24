/**
 * WhatsApp ↔ Claude CLI Bridge
 *
 * Run this service to connect WhatsApp Web with Claude Code CLI.
 * When you message yourself on WhatsApp, this bot forwards the message
 * to Claude and sends back the text response plus any generated images.
 *
 * Environment variables:
 *   CLAUDE_WORK_DIR    - Working directory for Claude (default: parent of this script)
 *   CLAUDE_TIMEOUT_MS  - Max time to wait for Claude (default: 1200000 = 20 min)
 */

const { Client, LocalAuth, MessageMedia } = require('whatsapp-web.js');
const qrcodeTerminal = require('qrcode-terminal');
const QRCode = require('qrcode');
const { spawn, exec } = require('child_process');
const util = require('util');
const execPromise = util.promisify(exec);
const fs = require('fs').promises;
const path = require('path');

// ─── Configuration ───
const WORK_DIR = process.env.CLAUDE_WORK_DIR || path.resolve(__dirname, '..');
const CLAUDE_TIMEOUT_MS = parseInt(process.env.CLAUDE_TIMEOUT_MS || '1200000', 10); // 20 min default for image-gen tasks

const IMAGE_EXTENSIONS = new Set(['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg']);
const EXCLUDED_DIRS = new Set([
  'node_modules', '.git', '.wwebjs_auth', 'whatsapp-bridge',
  'Library', 'Temp', 'Logs', 'obj', 'bin', '.vs', '.vscode',
  'UserSettings', 'PackageCache', '.agents', '.dotnet', '.dotnet-home',
  'BuildInstructions', 'BurstCache', 'PlayModeViewStates',
  'AtlasCache', 'Bee', 'Artifacts', 'ScriptAssemblies'
]);

// Every bot reply is prefixed with this. Any fromMe message starting with it is ignored.
const BOT_PREFIX = '🤖🤖 ';

let myId = null;
const sessions = new Map(); // chatId → Claude session ID for memory continuity
const busyChats = new Set(); // chatIds currently being processed

// ─── WhatsApp Client ───
const client = new Client({
  authStrategy: new LocalAuth({ dataPath: path.join(__dirname, '.wwebjs_auth') }),
  puppeteer: {
    headless: true,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-accelerated-2d-canvas',
      '--disable-gpu'
    ]
  }
});

client.on('qr', async (qr) => {
  console.log('\n╔══════════════════════════════════════════════════════════════╗');
  console.log('║  Scan the QR code below with WhatsApp on your phone         ║');
  console.log('║  (Settings → Linked Devices → Link a Device)                ║');
  console.log('╚══════════════════════════════════════════════════════════════╝\n');
  qrcodeTerminal.generate(qr, { small: true });

  try {
    const qrPath = path.join(__dirname, 'qr-code.png');
    await QRCode.toFile(qrPath, qr, { width: 500, margin: 2 });
    console.log(`\nQR code also saved to: ${qrPath}\n`);
  } catch (err) {
    console.error('Failed to save QR code image:', err.message);
  }
});

client.on('authenticated', () => {
  console.log('WhatsApp authenticated successfully.');
});

client.on('loading_screen', (percent, message) => {
  console.log(`Loading: ${percent}% - ${message}`);
});

client.on('change_state', (state) => {
  console.log('Connection state changed:', state);
});

client.on('auth_failure', (msg) => {
  console.error('WhatsApp authentication failure:', msg);
});

client.on('ready', () => {
  myId = client.info.wid._serialized;
  console.log('\n╔══════════════════════════════════════════════════════════════╗');
  console.log('║  🤖 WhatsApp Claude Bridge is ready!                        ║');
  console.log(`║  Your number: ${myId.padEnd(45)} ║`);
  console.log(`║  Working dir:  ${WORK_DIR.padEnd(45)} ║`);
  console.log('║                                                              ║');
  console.log('║  Message yourself on WhatsApp to talk to Claude.             ║');
  console.log('╚══════════════════════════════════════════════════════════════╝\n');
});

client.on('disconnected', (reason) => {
  console.log('WhatsApp disconnected:', reason);
  process.exit(0);
});

// ─── Message Handler ───
// We use 'message_create' because 'message' often does NOT fire for self-messages.
client.on('message_create', async (msg) => {
  const timestamp = new Date().toISOString();

  // Debug log every message_create so we can diagnose issues
  console.log(`[${timestamp}] 🔍 message_create | from:${msg.from} to:${msg.to} fromMe:${msg.fromMe} type:${msg.type} id:${msg.id?._serialized || 'n/a'} body:"${(msg.body || '').substring(0, 60)}"`);

  try {
    // Skip messages not sent by me
    if (!msg.fromMe) {
      console.log(`[${timestamp}] 🚫 Not from me (fromMe=false)`);
      return;
    }

    // Skip bot replies — they start with BOT_PREFIX
    if (msg.body && msg.body.startsWith(BOT_PREFIX)) {
      console.log(`[${timestamp}] 🚫 Bot reply, ignoring`);
      return;
    }

    // Only process messages in the "Me" chat
    // fromMe=true includes replies to any contact; contact.isMe is the reliable self-chat check.
    let chat;
    try {
      chat = await msg.getChat();
      const contact = await chat.getContact();
      if (!contact || !contact.isMe) {
        console.log(`[${timestamp}] 🚫 Not the Me chat (isMe=${contact?.isMe})`);
        return;
      }
    } catch (err) {
      console.log(`[${timestamp}] 🚫 Could not verify chat: ${err.message}`);
      return;
    }

    // Only plain text
    if (msg.type !== 'chat') {
      console.log(`[${timestamp}] 🚫 Not a chat message (type=${msg.type})`);
      return;
    }

    const userMessage = (msg.body || '').trim();
    if (!userMessage) {
      console.log(`[${timestamp}] 🚫 Empty body`);
      return;
    }

    console.log(`[${timestamp}] 📩 Processing: ${userMessage.substring(0, 100)}${userMessage.length > 100 ? '...' : ''}`);

    const chatId = chat.id._serialized;

    // Reject concurrent requests for the same chat
    if (busyChats.has(chatId)) {
      console.log(`[${timestamp}] ⏳ Already processing for this chat — rejecting`);
      try { await msg.react('⏳'); } catch {}
      return;
    }

    // Acknowledge receipt immediately
    try { await msg.react('👀'); } catch {}
    busyChats.add(chatId);

    try {
      await chat.sendStateTyping();

      // Snapshot images before running Claude
      const beforeImages = await scanImages(WORK_DIR);
      console.log(`[${timestamp}] 📸 Before scan: ${beforeImages.size} images`);

      // Run Claude (resume prior session if one exists for this chat)
      const runStartTime = Date.now();
      const sessionId = sessions.get(chatId);
      const claudeResult = await runClaude(userMessage, sessionId);
      const responseText = claudeResult.text;
      if (claudeResult.sessionId) {
        sessions.set(chatId, claudeResult.sessionId);
        console.log(`[${timestamp}] 💾 Session saved: ${claudeResult.sessionId}`);
      }

    // Snapshot images after running Claude
    const afterImages = await scanImages(WORK_DIR);
    console.log(`[${timestamp}] 📸 After scan: ${afterImages.size} images`);

    // Find brand-new or recently-modified image files
    const newImages = [];
    for (const [filePath, info] of afterImages) {
      const before = beforeImages.get(filePath);
      if (!before) {
        // Brand new file
        newImages.push(filePath);
      } else if (info.mtimeMs > runStartTime) {
        // Existing file but modified during this run
        newImages.push(filePath);
      }
    }

    if (newImages.length > 0) {
      console.log(`[${timestamp}] 🖼️  Detected ${newImages.length} image(s) to send:`);
      newImages.forEach(p => console.log(`   → ${p}`));
    } else {
      console.log(`[${timestamp}] 🖼️  No new or modified images detected`);
    }

    // Stop typing indicator
    await chat.clearState();

    // Send text response
    if (responseText) {
      await sendBotMessage(chat, responseText, timestamp);
    }

    // Send any newly created or modified images
    if (newImages.length > 0) {
      for (const imgPath of newImages) {
        try {
          await sendBotImage(chat, imgPath, timestamp);
        } catch (err) {
          console.error(`[${timestamp}] ❌ Failed to send image ${imgPath}:`, err.message);
          try {
            await sendBotMessage(chat, `Image ready but could not send: ${path.basename(imgPath)}`, timestamp);
          } catch (e2) {
            console.error(`[${timestamp}] ❌ Also failed to send image-fallback text:`, e2.message);
          }
        }
      }
    }

    // If the user asked to see images, also send uncommitted / recent local images
    const isImageShowRequest = /\b(show|send|view|see|display|list)\b.*\b(image|images|picture|pictures|art|card|cards)\b/i.test(userMessage)
      || /\b(image|images|picture|pictures|art|card|cards)\b.*\b(show|send|view|see|display|list)\b/i.test(userMessage);

    if (isImageShowRequest) {
      console.log(`[${timestamp}] 🖼️  User asked to see images; searching for local/uncommitted images...`);
      let showImages = await findUncommittedImages(WORK_DIR);
      if (showImages.length === 0) {
        showImages = await findRecentImages(WORK_DIR, 48); // fallback: last 48 hours
      }
      // Deduplicate against images already sent in this turn
      showImages = showImages.filter(p => !newImages.includes(p));
      if (showImages.length > 0) {
        const limit = 20;
        const toSend = showImages.slice(0, limit);
        console.log(`[${timestamp}] 🖼️  Sending ${toSend.length} local image(s)${showImages.length > limit ? ` (out of ${showImages.length})` : ''}:`);
        toSend.forEach(p => console.log(`   → ${p}`));
        for (const imgPath of toSend) {
          try {
            await sendBotImage(chat, imgPath, timestamp);
          } catch (err) {
            console.error(`[${timestamp}] ❌ Failed to send image ${imgPath}:`, err.message);
          }
        }
      } else {
        console.log(`[${timestamp}] 🖼️  No local/uncommitted images found`);
      }
    }

      if (!responseText && newImages.length === 0) {
        await sendBotMessage(chat, 'No output generated.', timestamp);
      }
    } finally {
      busyChats.delete(chatId);
    }
  } catch (error) {
    console.error(`[${timestamp}] ❌ Error in message handler:`, error.message);
    console.error(error.stack);

    try {
      const chat = await msg.getChat();
      await chat.clearState();
      let errorText = error.message && error.message.includes('timeout')
        ? 'Claude took too long to respond.'
        : (error.message || 'Unknown error occurred.');
      await sendBotMessage(chat, errorText, timestamp);
    } catch (sendErr) {
      console.error('Failed to send error message:', sendErr.message);
    }
  }
});

// ─── Bot Send Helpers ───
async function sendBotMessage(chat, text, timestamp) {
  const prefixed = BOT_PREFIX + text;
  const sent = await chat.sendMessage(prefixed);
  console.log(`[${timestamp}] 📤 Sent text (${prefixed.length} chars)`);
  return sent;
}

async function sendBotImage(chat, imgPath, timestamp) {
  const media = MessageMedia.fromFilePath(imgPath);
  const caption = path.basename(imgPath);
  const sent = await chat.sendMessage(media, { caption });
  console.log(`[${timestamp}] 📤 Sent image: ${imgPath}`);
  return sent;
}

// ─── Image Discovery Helpers ───

// Find image files that are untracked or modified in Git (not committed)
async function findUncommittedImages(dir) {
  try {
    const { stdout } = await execPromise('git status --porcelain', { cwd: dir });
    if (!stdout.trim()) return [];

    const images = [];
    for (const line of stdout.split(/\r?\n/)) {
      if (!line.trim()) continue;
      // Format: XY path  or  XY orig_path -> path
      const status = line.substring(0, 2);
      let filePath = line.substring(3);
      // Handle rename arrows
      const arrowIdx = filePath.indexOf(' -> ');
      if (arrowIdx !== -1) {
        filePath = filePath.substring(arrowIdx + 4);
      }
      filePath = filePath.trim();
      if (!filePath) continue;

      const ext = path.extname(filePath).toLowerCase();
      if (!IMAGE_EXTENSIONS.has(ext)) continue;
      // Skip .meta files if there's a corresponding image (Unity meta)
      if (ext === '.meta') continue;

      const fullPath = path.resolve(dir, filePath);
      images.push(fullPath);
    }
    return images;
  } catch (err) {
    console.error('findUncommittedImages failed:', err.message);
    return [];
  }
}

// Find image files modified within the last N hours (fallback when Git finds nothing)
async function findRecentImages(dir, hours) {
  const images = [];
  const cutoff = Date.now() - (hours * 60 * 60 * 1000);

  async function scan(currentDir) {
    let entries;
    try {
      entries = await fs.readdir(currentDir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      const fullPath = path.join(currentDir, entry.name);
      if (entry.isDirectory()) {
        const baseName = entry.name;
        if (!EXCLUDED_DIRS.has(baseName) && !baseName.startsWith('.')) {
          await scan(fullPath);
        }
      } else if (entry.isFile()) {
        const ext = path.extname(entry.name).toLowerCase();
        if (IMAGE_EXTENSIONS.has(ext)) {
          try {
            const stat = await fs.stat(fullPath);
            if (stat.mtimeMs > cutoff) {
              images.push(fullPath);
            }
          } catch {
            // ignore
          }
        }
      }
    }
  }

  await scan(dir);
  // Sort by most recent first
  const imagesWithMtime = [];
  for (const imgPath of images) {
    try {
      const stat = await fs.stat(imgPath);
      imagesWithMtime.push({ path: imgPath, mtime: stat.mtimeMs });
    } catch {
      // ignore unreadable
    }
  }
  imagesWithMtime.sort((a, b) => b.mtime - a.mtime);
  return imagesWithMtime.map(x => x.path);
}

// ─── Run Claude Subprocess ───
function runClaude(prompt, sessionId = null) {
  return new Promise((resolve, reject) => {
    const args = ['--output-format', 'json', '--dangerously-skip-permissions'];

    if (sessionId) {
      args.push('--resume', sessionId);
    }

    args.push('-p', prompt);

    console.log(`Running: claude ${args.map(a => (a.includes(' ') ? `"${a}"` : a)).join(' ')}`);

    const child = spawn('claude', args, {
      cwd: WORK_DIR,
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env }
    });

    let stdout = '';
    let stderr = '';
    let killedByTimeout = false;

    child.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    child.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    const timer = setTimeout(() => {
      killedByTimeout = true;
      child.kill('SIGTERM');
    }, CLAUDE_TIMEOUT_MS);

    child.on('close', (code, signal) => {
      clearTimeout(timer);

      if (killedByTimeout) {
        reject(new Error(`claude timed out after ${CLAUDE_TIMEOUT_MS}ms`));
        return;
      }

      if (stderr) {
        console.error('Claude stderr:', stderr.trim());
      }

      if (code === 0 || code === null) {
        resolve(parseClaudeOutput(stdout));
      } else {
        reject(new Error(`claude exited with code ${code}. stderr: ${stderr.trim()}`));
      }
    });

    child.on('error', (err) => {
      clearTimeout(timer);
      reject(new Error(`Failed to start claude: ${err.message}`));
    });
  });
}

// Parse JSON output from claude --output-format json
// Returns { text, sessionId }
function parseClaudeOutput(raw) {
  const normalized = raw.replace(/\r\n/g, '\n').trim();

  // Claude CLI with --output-format json may emit newline-delimited JSON objects.
  // The result object has type="result"; earlier lines are tool-use events.
  const lines = normalized.split('\n');
  let text = '';
  let outSessionId = null;

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    try {
      const obj = JSON.parse(trimmed);
      if (obj.session_id) outSessionId = obj.session_id;
      if (obj.type === 'result' && obj.result !== undefined) {
        text = String(obj.result);
      }
    } catch {}
  }

  // Fallback: try the entire output as one JSON object
  if (!text && normalized) {
    try {
      const obj = JSON.parse(normalized);
      text = String(obj.result ?? obj.message ?? '');
      outSessionId = outSessionId || obj.session_id || null;
    } catch {
      // Last resort: treat raw stdout as plain text
      text = normalized;
    }
  }

  return { text, sessionId: outSessionId };
}

// ─── Image Scanner ───
async function scanImages(rootDir) {
  const images = new Map();

  async function scan(dir) {
    let entries;
    try {
      entries = await fs.readdir(dir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);

      if (entry.isDirectory()) {
        const baseName = entry.name;
        if (!EXCLUDED_DIRS.has(baseName) && !baseName.startsWith('.')) {
          await scan(fullPath);
        }
      } else if (entry.isFile()) {
        const ext = path.extname(entry.name).toLowerCase();
        if (IMAGE_EXTENSIONS.has(ext)) {
          try {
            const stat = await fs.stat(fullPath);
            images.set(fullPath, { size: stat.size, mtimeMs: stat.mtimeMs });
          } catch {
            // ignore unreadable files
          }
        }
      }
    }
  }

  await scan(rootDir);
  return images;
}

// ─── Catch silent crashes ───
process.on('unhandledRejection', (reason, promise) => {
  console.error('Unhandled Rejection at:', promise, 'reason:', reason);
});

process.on('uncaughtException', (err) => {
  console.error('Uncaught Exception:', err);
});

// ─── Graceful Shutdown ───
process.on('SIGINT', async () => {
  console.log('\nShutting down WhatsApp bridge...');
  await client.destroy();
  process.exit(0);
});

process.on('SIGTERM', async () => {
  console.log('\nShutting down WhatsApp bridge...');
  await client.destroy();
  process.exit(0);
});

// ─── Start ───
console.log('Starting WhatsApp Claude Bridge...');
client.initialize();
