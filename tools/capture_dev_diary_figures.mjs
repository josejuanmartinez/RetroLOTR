import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";

const root = process.cwd();
const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const article = `file:///${path.join(root, "dev-diary-video-games-ai-and-some-generative-ai.html").replaceAll("\\", "/")}`;
const outputDir = path.join(root, "dev-diary-assets", "medium-figures");
await mkdir(outputDir, { recursive: true });

const port = 9333;
const browser = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--hide-scrollbars",
  "--allow-file-access-from-files",
  `--remote-debugging-port=${port}`,
  "--window-size=1440,1100",
  article,
], { stdio: "ignore", windowsHide: true });

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let target;
for (let attempt = 0; attempt < 60; attempt++) {
  try {
    const pages = await fetch(`http://127.0.0.1:${port}/json`).then(r => r.json());
    target = pages.find(page => page.type === "page" && page.url.includes("dev-diary"));
    if (target) break;
  } catch {}
  await pause(250);
}

if (!target) {
  browser.kill();
  throw new Error("Could not connect to the headless Chrome page.");
}

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

let nextId = 1;
const pending = new Map();
socket.addEventListener("message", event => {
  const message = JSON.parse(event.data);
  if (!message.id || !pending.has(message.id)) return;
  const { resolve, reject } = pending.get(message.id);
  pending.delete(message.id);
  if (message.error) reject(new Error(message.error.message));
  else resolve(message.result);
});

function command(method, params = {}) {
  const id = nextId++;
  socket.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

await command("Page.enable");
await command("Runtime.enable");
await command("Emulation.setDeviceMetricsOverride", {
  width: 1440,
  height: 1100,
  deviceScaleFactor: 2,
  mobile: false,
});
await pause(2500);

// The article's older HTN mockup illustrates a two-step build/trade method that the
// current default strategy no longer ships. Rewrite only the in-browser copy used for
// this exported figure so it reflects the current recovery picker and task ids.
await command("Runtime.evaluate", {
  expression: `(() => {
    const heading = document.querySelector('#hierarchical-task-network');
    let widget = heading && heading.nextElementSibling;
    while (widget && !widget.classList.contains('widget')) widget = widget.nextElementSibling;
    if (!widget) return;
    widget.querySelector('.widget-bar').innerHTML = '<span class="dot"></span>ai widget - strategies tab (current default, excerpt)';
    widget.querySelector('.rows').innerHTML = ` + "`" + `
      <div class="row"><span class="pill type">CompoundTask</span><span class="taskid">root</span></div>
      <div class="row" style="padding-left:20px"><span class="conn">TRY</span><span class="pill type">Method</span><span class="taskid">root.recover</span><span class="field">WHEN <b>EconomyCritical OR EconomyWeak</b></span></div>
      <div class="row" style="padding-left:40px"><span class="conn">DO</span><span class="pill type">CompoundTask</span><span class="taskid">root.recover.pick</span></div>
      <div class="row" style="padding-left:60px"><span class="conn">TRY</span><span class="pill type">Method</span><span class="taskid">...pick.mithril</span><span class="field">WHEN <b>MithrilReady</b></span></div>
      <div class="row" style="padding-left:80px"><span class="conn">DO</span><span class="pill type">Primitive</span><span class="taskid">...mithril.leaf</span><span class="field art">prefer <b>MithrilInsufficient / MithrilSurplus</b></span><span class="field">UNTIL <b>Never</b></span></div>
      <div class="row" style="padding-left:60px"><span class="conn">OR ELSE</span><span class="pill type">Method</span><span class="taskid">...pick.timber</span><span class="field">WHEN <b>TimberReady</b></span></div>
      <div class="row" style="padding-left:80px"><span class="conn">DO</span><span class="pill type">Primitive</span><span class="taskid">...timber.leaf</span><span class="field art">prefer <b>TimberInsufficient / TimberSurplus</b></span><span class="field">UNTIL <b>Never</b></span></div>
      <div class="row" style="padding-left:20px"><span class="conn">OR ELSE</span><span class="pill type">Method</span><span class="taskid">root.offense</span><span class="field">WHEN <b>OffenseWinRatioReady</b></span></div>
      <div class="row" style="padding-left:40px"><span class="conn">DO</span><span class="pill type">CompoundTask</span><span class="taskid">root.offense.pick</span></div>
      <div class="row" style="padding-left:20px"><span class="conn">OR ELSE</span><span class="pill type">Method</span><span class="taskid">root.fallback</span><span class="field">WHEN <b>Always</b></span></div>
      <div class="row" style="padding-left:40px"><span class="conn">DO</span><span class="pill type">Primitive</span><span class="taskid">root.fallback.leaf</span><span class="field art">bias <b>-</b></span><span class="field">UNTIL <b>Never</b></span></div>
    ` + "`" + `;

    const replaceText = (root, from, to) => {
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
      while (walker.nextNode()) walker.currentNode.nodeValue = walker.currentNode.nodeValue.replaceAll(from, to);
    };
    const bars = [...document.querySelectorAll('.widget-bar')];
    const economyExample = bars.find(x => x.textContent.includes('worked example 1'))?.parentElement;
    const offenseExample = bars.find(x => x.textContent.includes('worked example 2'))?.parentElement;
    if (economyExample) replaceText(economyExample, 'root.recover.build', 'root.recover.pick.iron.leaf');
    if (offenseExample) replaceText(offenseExample, 'root.offense.pick.mil.leaf', 'root.offense.pick.attack.leaf');

    const mlpFigure = document.createElement('figure');
    mlpFigure.id = 'export-mlp-architecture';
    mlpFigure.className = 'diagram-figure';
    mlpFigure.innerHTML = ` + "`" + `
      <svg viewBox="0 0 920 360" role="img" aria-label="MLP scorer pipeline from game state, HTN plan, and candidate card through encoded features and three dense layers to one learned candidate score.">
        <defs><marker id="mlp-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,0 L10,5 L0,10 Z" fill="currentColor"/></marker></defs>
        <text x="460" y="28" text-anchor="middle" font-size="16" letter-spacing="2" fill="var(--accent-logic-strong)">THE MLP RANKS; IT DOES NOT PLAN</text>
        <rect x="22" y="60" width="190" height="64" fill="none" stroke="currentColor"/>
        <text x="117" y="84" text-anchor="middle" font-size="12" fill="currentColor">PRE-ACTION STATE</text>
        <text x="117" y="104" text-anchor="middle" font-size="10" opacity=".72" fill="currentColor">skills · army · resources · distances</text>
        <rect x="22" y="144" width="190" height="64" fill="none" stroke="currentColor"/>
        <text x="117" y="168" text-anchor="middle" font-size="12" fill="currentColor">ACTIVE PLAN</text>
        <text x="117" y="188" text-anchor="middle" font-size="10" opacity=".72" fill="currentColor">alignment · HTN task · target</text>
        <rect x="22" y="228" width="190" height="64" fill="none" stroke="currentColor"/>
        <text x="117" y="252" text-anchor="middle" font-size="12" fill="currentColor">CANDIDATE CARD</text>
        <text x="117" y="272" text-anchor="middle" font-size="10" opacity=".72" fill="currentColor">advisor · cost · requirements · difficulty</text>
        <path d="M212 92 C250 92 250 176 282 176" fill="none" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <path d="M212 176 L282 176" fill="none" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <path d="M212 260 C250 260 250 176 282 176" fill="none" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <rect x="286" y="126" width="132" height="100" fill="none" stroke="var(--accent-logic-strong)"/>
        <text x="352" y="162" text-anchor="middle" font-size="12" fill="var(--accent-logic-strong)">FEATURE VECTOR</text>
        <text x="352" y="183" text-anchor="middle" font-size="10" opacity=".8" fill="var(--accent-logic-strong)">normalise · encode</text>
        <text x="352" y="201" text-anchor="middle" font-size="10" opacity=".8" fill="var(--accent-logic-strong)">mask missing values</text>
        <line x1="418" y1="176" x2="464" y2="176" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <rect x="468" y="112" width="78" height="128" fill="none" stroke="currentColor"/>
        <text x="507" y="166" text-anchor="middle" font-size="12" fill="currentColor">DENSE</text><text x="507" y="188" text-anchor="middle" font-size="16" fill="var(--accent-art-strong)">128</text><text x="507" y="211" text-anchor="middle" font-size="10" opacity=".72" fill="currentColor">ReLU</text>
        <line x1="546" y1="176" x2="574" y2="176" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <rect x="578" y="126" width="72" height="100" fill="none" stroke="currentColor"/>
        <text x="614" y="166" text-anchor="middle" font-size="12" fill="currentColor">DENSE</text><text x="614" y="188" text-anchor="middle" font-size="16" fill="var(--accent-art-strong)">64</text><text x="614" y="209" text-anchor="middle" font-size="10" opacity=".72" fill="currentColor">ReLU</text>
        <line x1="650" y1="176" x2="678" y2="176" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <rect x="682" y="140" width="68" height="72" fill="none" stroke="currentColor"/>
        <text x="716" y="169" text-anchor="middle" font-size="12" fill="currentColor">DENSE</text><text x="716" y="191" text-anchor="middle" font-size="16" fill="var(--accent-art-strong)">32</text>
        <line x1="750" y1="176" x2="784" y2="176" stroke="currentColor" marker-end="url(#mlp-arrow)"/>
        <rect x="788" y="126" width="110" height="100" fill="none" stroke="var(--accent-logic-strong)"/>
        <text x="843" y="164" text-anchor="middle" font-size="12" fill="var(--accent-logic-strong)">ONE VALUE</text>
        <text x="843" y="188" text-anchor="middle" font-size="15" fill="var(--accent-logic-strong)">learned score</text>
        <text x="460" y="330" text-anchor="middle" font-size="11" fill="currentColor" opacity=".76">Blackboard remembers · HTN plans · rules filter legality · the MLP contributes one ranking signal</text>
      </svg>
      <figcaption><span class="fig-label">Proposed scorer</span>Only pre-action state and candidate features enter the network. Selection, curation, and observed outcomes remain labels—not inputs.</figcaption>` + "`" + `;
    document.querySelector('.page').appendChild(mlpFigure);
  })()`,
});

const figures = [
  {
    filename: "01-behaviour-tree.png",
    expression: `(() => {
      const heading = document.querySelector('#foundations');
      return heading ? heading.parentElement.querySelector('#foundations ~ .widget') : null;
    })()`,
  },
  {
    filename: "02-htn-planner.png",
    expression: `(() => {
      const heading = document.querySelector('#hierarchical-task-network');
      let node = heading && heading.nextElementSibling;
      while (node && !node.classList.contains('widget')) node = node.nextElementSibling;
      return node;
    })()`,
  },
  {
    filename: "03-one-blackboard-per-character.png",
    expression: `(() => {
      const heading = document.querySelector('#blackboard');
      let node = heading && heading.nextElementSibling;
      while (node && !node.classList.contains('diagram-figure')) node = node.nextElementSibling;
      return node;
    })()`,
  },
  {
    filename: "04-blackboard-inspector.png",
    expression: `(() => {
      const bars = [...document.querySelectorAll('.widget-bar')];
      return bars.find(x => x.textContent.includes('ai blackboard panel'))?.parentElement ?? null;
    })()`,
  },
  {
    filename: "05-mlp-training-record.png",
    expression: `(() => {
      const bars = [...document.querySelectorAll('.widget-bar')];
      return bars.find(x => x.textContent.includes('offline training record'))?.parentElement ?? null;
    })()`,
  },
  {
    filename: "06-real-card-candidates.png",
    expression: `(() => {
      const heading = document.querySelector('#advisors');
      let node = heading && heading.nextElementSibling;
      while (node && !node.classList.contains('card-row')) node = node.nextElementSibling;
      return node;
    })()`,
  },
  {
    filename: "07-utility-scoring-economy.png",
    expression: `(() => {
      const bars = [...document.querySelectorAll('.widget-bar')];
      return bars.find(x => x.textContent.includes('worked example 1'))?.parentElement ?? null;
    })()`,
  },
  {
    filename: "08-utility-scoring-offense.png",
    expression: `(() => {
      const bars = [...document.querySelectorAll('.widget-bar')];
      return bars.find(x => x.textContent.includes('worked example 2'))?.parentElement ?? null;
    })()`,
  },
  {
    filename: "09-mlp-architecture.png",
    expression: `document.querySelector('#export-mlp-architecture')`,
  },
];

for (const figure of figures) {
  const evaluated = await command("Runtime.evaluate", {
    expression: `(() => {
      const el = ${figure.expression};
      if (!el) return null;
      const r = el.getBoundingClientRect();
      return { x: r.left + scrollX, y: r.top + scrollY, width: r.width, height: r.height };
    })()`,
    returnByValue: true,
  });
  const rect = evaluated.result.value;
  if (!rect) throw new Error(`Could not find element for ${figure.filename}`);
  const paddingX = 22;
  const paddingTop = 22;
  const paddingBottom = 0;
  const capture = await command("Page.captureScreenshot", {
    format: "png",
    fromSurface: true,
    captureBeyondViewport: true,
    clip: {
      x: Math.max(0, rect.x - paddingX),
      y: Math.max(0, rect.y - paddingTop),
      width: rect.width + paddingX * 2,
      height: rect.height + paddingTop + paddingBottom,
      scale: 1,
    },
  });
  await writeFile(path.join(outputDir, figure.filename), Buffer.from(capture.data, "base64"));
}

socket.close();
browser.kill();
console.log(figures.map(x => path.join(outputDir, x.filename)).join("\n"));
