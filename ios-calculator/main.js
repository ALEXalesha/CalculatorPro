const { app, BrowserWindow, Menu, ipcMain } = require('electron');
const path = require('path');

let mainWindow = null;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 320,
    height: 640,
    // Минимум как у калькулятора Windows (320x500), по ширине чуть меньше.
    minWidth: 280,
    minHeight: 500,
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
  mainWindow.once('ready-to-show', () => mainWindow.show());

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
