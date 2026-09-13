// End-to-end smoke test: runs the real page in Electron (hidden window, same
// preload and CSP as the app), clicks buttons and checks what is displayed.
// Usage: npm run test:e2e   (writes a screenshot to $E2E_OUT or the temp dir)
const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');
const os = require('os');

const ROOT = path.join(__dirname, '..');
const errors = [];
let failures = 0;

function check(name, actual, expected) {
  const ok = actual === expected;
  if (!ok) failures++;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}: ${JSON.stringify(actual)}${ok ? '' : ' (expected ' + JSON.stringify(expected) + ')'}`);
}

app.whenReady().then(async () => {
  setTimeout(() => { console.error('timeout'); app.exit(2); }, 30000);
  const win = new BrowserWindow({
    width: 360, height: 700, show: false, frame: false, transparent: true,
    webPreferences: {
      preload: path.join(ROOT, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: true,
      // In-memory session: no localStorage (history) carried over from earlier runs.
      partition: `e2e-${process.pid}-${Date.now()}`,
    },
  });
  win.webContents.on('console-message', (e, level, message) => {
    const lvl = e.level !== undefined ? e.level : level;
    if (lvl === 'error' || lvl === 3) errors.push(e.message !== undefined ? e.message : message);
  });
  await win.loadFile(path.join(ROOT, 'src', 'index.html'));

  const js = code => win.webContents.executeJavaScript(code);
  const click = sel => js(`(() => { const el = document.querySelector(${JSON.stringify(sel)}); if (!el) throw new Error('no ' + ${JSON.stringify(sel)}); el.click(); })()`);
  const keys = async list => { for (const k of list) await click(`[data-key="${k}"]`); };
  const text = sel => js(`document.querySelector(${JSON.stringify(sel)}).textContent.trim()`);

  check('engine loaded under CSP', await js('typeof window.CalcCore'), 'object');
  check('preload bridge present', await js('typeof window.electronAPI'), 'object');
  check('standard keypad rendered', await js('document.querySelectorAll("#calcKeys [data-key]").length'), 20);

  await keys(['2', '+', '3', '×', '4', '=']);
  check('2+3×4=', await text('#mainResult'), '14');
  check('history line', await text('#historyLine'), '2+3×4 =');

  await click('#tabScientific');
  check('scientific keypad rendered', await js('document.querySelectorAll("#calcKeys [data-key]").length'), 48);
  await keys(['AC', '√', '1', '6', '=']);
  check('√16=', await text('#mainResult'), '4');
  await keys(['AC', '−', '2', 'xʸ', '2', '=']);
  check('−2^2=', await text('#mainResult'), '-4');

  await click('#tabFraction');
  await keys(['a/b', '1', 'a/b', '2', '+', 'a/b', '1', 'a/b', '3', '=']);
  check('1/2+1/3 as fraction', await js('document.querySelector("#mainResult .frac-num").textContent + "/" + document.querySelector("#mainResult .frac-den").textContent'), '5/6');

  await click('#tabStandard');
  await click('#btnHistory');
  check('history panel lists results', await js('document.querySelectorAll("#historyList .history-item").length'), 4);

  const out = process.env.E2E_OUT || path.join(os.tmpdir(), 'calcpro-glass-e2e.png');
  await click('#btnHistory');
  await keys(['AC', '1', '2', '3', '4', '5', '÷', '7', '=']);
  await new Promise(r => setTimeout(r, 400));
  fs.writeFileSync(out, (await win.webContents.capturePage()).toPNG());
  console.log('screenshot:', out);

  check('no console errors', errors.join(' | '), '');
  console.log(failures ? `${failures} check(s) failed` : 'all e2e checks passed');
  app.exit(failures ? 1 : 0);
}).catch(e => { console.error(e); app.exit(1); });
