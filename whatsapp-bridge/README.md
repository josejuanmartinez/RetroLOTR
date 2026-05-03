# WhatsApp ↔ Kimi Bridge

Talk to Kimi Code CLI by messaging yourself on WhatsApp. This bridge forwards your self-messages to Kimi and sends back the AI's text responses plus any images it generates.

## Prerequisites

- [Node.js](https://nodejs.org/) (v18+ recommended)
- [Kimi Code CLI](https://github.com/MoonshotAI/kimi-cli) installed and logged in (`kimi login`)
- WhatsApp on your phone

## Setup

```bash
cd whatsapp-bridge
npm install
```

## Usage

### Start the bridge

```bash
npm start
```

Or on Windows:

```bash
start-bridge.bat
```

### First run — authenticate

1. The terminal displays a **QR code** (also saved as `qr-code.png` in this folder).
2. Open WhatsApp on your phone → **Settings → Linked Devices → Link a Device**.
3. Scan the QR code.
4. The bridge saves the session, so you only need to scan once.

### Talk to Kimi

1. Open your **"Me"** chat (your own phone number) in WhatsApp.
2. Send any message — for example:
   - `Explain the deck system in my Unity project`
   - `Generate a new card image for a hobbit ally`
3. Kimi processes it and the bot replies with:
   - **Text** — Kimi's response
   - **Images** — any new images created in the working directory during the run

## Configuration

Set environment variables before starting:

| Variable | Default | Description |
|----------|---------|-------------|
| `KIMI_WORK_DIR` | Parent of `whatsapp-bridge` | Project directory Kimi works in |
| `KIMI_TIMEOUT_MS` | `1200000` (20 min) | Max time to wait for Kimi |

Example:

```powershell
$env:KIMI_WORK_DIR = "C:\Users\jjmca\RetroLOTR"
$env:KIMI_TIMEOUT_MS = "600000"
npm start
```

## How It Works

1. **Listen** — The bridge monitors your WhatsApp Web session.
2. **Filter** — Only messages you send to yourself (`fromMe`) are processed.
3. **Run Kimi** — It spawns `kimi --print --quiet --yolo --afk -p "<your message>"`.
4. **Detect images** — Before and after each run it scans the working directory for new image files (`.png`, `.jpg`, `.webp`, etc.).
5. **Reply** — Sends Kimi's text output back to your "Me" chat, followed by any newly created images.

## Preventing Loops

The bridge tracks message IDs of everything it sends and ignores them on receipt, so it never replies to its own messages.

## Stopping

Press `Ctrl + C` in the terminal. The bridge will gracefully close the WhatsApp session.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| QR code won't scan | Make sure your phone has an internet connection and try again. |
| "Failed to start kimi" | Ensure `kimi` is in your system `PATH`. Run `kimi --version` to verify. |
| Kimi times out | Increase `KIMI_TIMEOUT_MS` for long-running tasks. |
| Images not sent | Only **new** image files are sent. Existing files that are overwritten are ignored. |
| No response to self-message | The bridge now uses `message_create` (not `message`) to catch self-messages. Restart the bridge and check the terminal — you should see a `🔍 message_create` log line when you send a message. If `fromMe` is `false`, the bridge filters it out. |
| Session lost | Delete the `.wwebjs_auth` folder and scan the QR code again. |
