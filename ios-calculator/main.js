const { app, BrowserWindow, screen, Menu, ipcMain } = require('electron');
const path = require('path');
const WindowState = require('./window-state');

let mainWindow = null;

// Размер и место окна между запусками: %APPDATA%\<приложение>\window-state.json.
const SIZE = { width: 320, height: 640, minWidth: 280, minHeight: 500 };
const stateFile = () => path.join(app.getPath('userData'), 'window-state.json');

function createWindow() {
  // Основной экран первым: на нём окно встанет по центру, если сохранённое место не видно.
  const primary = screen.getPrimaryDisplay();
  const areas = [primary, ...screen.getAllDisplays().filter((d) => d.id !== primary.id)].map((d) => d.workArea);
  const placed = WindowState.restore(WindowState.load(stateFile()), areas, SIZE);

  mainWindow = new BrowserWindow({
    ...(placed.x !== undefined ? { x: placed.x, y: placed.y } : {}),
    width: placed.width,
    height: placed.height,
    // Минимум как у калькулятора Windows (320x500), по ширине чуть меньше.
    minWidth: SIZE.minWidth,
    minHeight: SIZE.minHeight,
    frame: false,
    transparent: true,
    hasShadow: true,
    resizable: true,
    maximizable: true,
    minimizable: true,
    title: 'Calculator iOS 26',
    icon: path.join(__dirname, 'assets', 'icon.ico'),
    show: false,
    webPreferences: {
      // The renderer talks to the main process only through preload.js
      // (window.windowAPI); it has no Node.js access of its own.
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      backgroundThrottling: false,
    },
  });

  mainWindow.loadFile(path.join(__dirname, 'src', 'index.html'));
  if (placed.maximized) mainWindow.maximize();
  mainWindow.once('ready-to-show', () => mainWindow.show());

  // Запись после каждого перемещения и изменения размера (события приходят один раз в
  // конце перетаскивания) и при закрытии: если процесс убьют, размер не потеряется.
  const remember = () => { if (mainWindow && !mainWindow.isDestroyed() && !mainWindow.isMinimized()) WindowState.save(stateFile(), WindowState.capture(mainWindow)); };
  for (const event of ['resized', 'moved', 'maximize', 'unmaximize']) mainWindow.on(event, remember);
  mainWindow.on('close', () => { if (mainWindow) WindowState.save(stateFile(), WindowState.capture(mainWindow)); });

  // F12 / Ctrl+Shift+I — DevTools, only when running from sources (npm start).
  if (!app.isPackaged) {
    mainWindow.webContents.on('before-input-event', (event, input) => {
      if (input.key === 'F12' || (input.control && input.shift && input.key.toLowerCase() === 'i')) {
        mainWindow.webContents.toggleDevTools();
        event.preventDefault();
      }
    });
  }

  mainWindow.on('closed', () => { mainWindow = null; });
}

const senderWindow = event => BrowserWindow.fromWebContents(event.sender);

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  Menu.setApplicationMenu(null);

  app.on('second-instance', () => {
    if (!mainWindow) return;
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.focus();
  });

  ipcMain.on('window-minimize', event => { const w = senderWindow(event); if (w) w.minimize(); });
  ipcMain.on('window-close', event => { const w = senderWindow(event); if (w) w.close(); });
  ipcMain.on('window-maximize-toggle', event => {
    const w = senderWindow(event);
    if (!w) return;
    if (w.isMaximized()) w.unmaximize();
    else w.maximize();
  });

  app.whenReady().then(createWindow);
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
  app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
}
