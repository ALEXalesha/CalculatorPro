// Автотест: запускает Electron, симулирует РЕАЛЬНЫЕ mouse события через sendInputEvent
// (проходят через OS-level hit-testing для drag regions)
const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');

const log = (msg) => {
  const line = `[${new Date().toISOString()}] ${msg}\n`;
  fs.appendFileSync('test-output.log', line);
  console.log(line.trim());
};

fs.writeFileSync('test-output.log', '');

let win;
let minimizeReceived = false;
let closeReceived = false;

ipcMain.on('window-minimize', () => {
  minimizeReceived = true;
  log('IPC RECEIVED: window-minimize');
});
ipcMain.on('window-close', () => {
  closeReceived = true;
  log('IPC RECEIVED: window-close');
});

async function realClick(rect) {
  const x = Math.round(rect.x + rect.width / 2);
  const y = Math.round(rect.y + rect.height / 2);
  log(`real click at (${x}, ${y})`);
  win.webContents.sendInputEvent({ type: 'mouseMove', x, y });
  win.webContents.sendInputEvent({ type: 'mouseDown', x, y, button: 'left', clickCount: 1 });
  win.webContents.sendInputEvent({ type: 'mouseUp', x, y, button: 'left', clickCount: 1 });
  await new Promise(r => setTimeout(r, 400));
}

app.whenReady().then(async () => {
  win = new BrowserWindow({
    width: 420, height: 860,
    frame: false, transparent: true,
    show: true,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false,
      sandbox: false,
    },
  });

  win.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  win.webContents.on('console-message', (event, level, message) => {
    if (level === 3 || level === 2) log(`[renderer] ${message}`);
  });

  win.webContents.on('did-finish-load', async () => {
    log('renderer loaded');
    await new Promise(r => setTimeout(r, 800));

    // получаем координаты
    const minRect = await win.webContents.executeJavaScript(
      `(() => { const r = document.getElementById('winMin').getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; })()`
    );
    const closeRect = await win.webContents.executeJavaScript(
      `(() => { const r = document.getElementById('winClose').getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; })()`
    );
    log('minRect: ' + JSON.stringify(minRect));
    log('closeRect: ' + JSON.stringify(closeRect));

    // получаем элемент в этой точке (что РЕАЛЬНО под курсором)
    const minHit = await win.webContents.executeJavaScript(
      `(() => { const e = document.elementFromPoint(${minRect.x + minRect.width/2}, ${minRect.y + minRect.height/2}); return e ? e.tagName + '#' + e.id + '.' + e.className : 'none'; })()`
    );
    const closeHit = await win.webContents.executeJavaScript(
      `(() => { const e = document.elementFromPoint(${closeRect.x + closeRect.width/2}, ${closeRect.y + closeRect.height/2}); return e ? e.tagName + '#' + e.id + '.' + e.className : 'none'; })()`
    );
    log(`elementFromPoint(min center): ${minHit}`);
    log(`elementFromPoint(close center): ${closeHit}`);

    // получаем computed -webkit-app-region на каждом
    const minRegion = await win.webContents.executeJavaScript(
      `getComputedStyle(document.getElementById('winMin'))['-webkit-app-region'] || getComputedStyle(document.getElementById('winMin')).appRegion || 'unknown'`
    );
    log(`winMin computed app-region: ${minRegion}`);

    log('--- REAL CLICK TEST ---');
    minimizeReceived = false;
    closeReceived = false;

    await realClick(minRect);
    log(`after real click min: minimizeReceived=${minimizeReceived}`);

    // window may be minimized - restore for second test
    win.restore();
    await new Promise(r => setTimeout(r, 300));

    // re-fetch close rect (may have shifted)
    const closeRect2 = await win.webContents.executeJavaScript(
      `(() => { const r = document.getElementById('winClose').getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; })()`
    );
    await realClick(closeRect2);
    log(`after real click close: closeReceived=${closeReceived}`);

    log('=== RESULTS ===');
    log(`min: ${minimizeReceived}, close: ${closeReceived}`);
    setTimeout(() => app.quit(), 500);
  });
});

app.on('window-all-closed', () => app.quit());
