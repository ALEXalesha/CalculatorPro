const { app, BrowserWindow, screen, Menu, ipcMain } = require('electron');
const path = require('path');
const WindowState = require('./window-state');

let win = null;

// Размер и место окна между запусками: %APPDATA%\<приложение>\window-state.json.
const SIZE = { width: 320, height: 640, minWidth: 280, minHeight: 500 };
const stateFile = () => path.join(app.getPath('userData'), 'window-state.json');

function createWindow() {
  // Основной экран первым: на нём окно встанет по центру, если сохранённое место не видно.
  const primary = screen.getPrimaryDisplay();
  const areas = [primary, ...screen.getAllDisplays().filter((d) => d.id !== primary.id)].map((d) => d.workArea);
  const placed = WindowState.restore(WindowState.load(stateFile()), areas, SIZE);

  win = new BrowserWindow({
    ...(placed.x !== undefined ? { x: placed.x, y: placed.y } : {}),
    width: placed.width,
    height: placed.height,
    // Минимум как у калькулятора Windows (320x500), по ширине чуть меньше.
    minWidth: SIZE.minWidth,
    minHeight: SIZE.minHeight,
    frame: false,
    transparent: true,
    // Своё скругление (26 px, в CSS) и без системных рамки и тени: Windows 11 рисовала
    // вокруг прозрачного окна свою рамку со скруглением 8 px и прямоугольную тень, и на
    // углах они торчали за нашим скруглением.
    hasShadow: false,
    roundedCorners: false,
    resizable: true,
    backgroundColor: '#00000000',
    title: 'Calc Pro Glass',
    icon: path.join(__dirname, 'build', 'icon.ico'),
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  win.setMenuBarVisibility(false);
  win.loadFile(path.join(__dirname, 'src', 'index.html'));
  if (placed.maximized) win.maximize();
  win.once('ready-to-show', () => win.show());

  // Запись после каждого перемещения и изменения размера (события приходят один раз в
  // конце перетаскивания) и при закрытии: если процесс убьют, размер не потеряется.
  const remember = () => { if (win && !win.isDestroyed() && !win.isMinimized()) WindowState.save(stateFile(), WindowState.capture(win)); };
  for (const event of ['resized', 'moved', 'maximize', 'unmaximize']) win.on(event, remember);
  win.on('close', () => { if (win) WindowState.save(stateFile(), WindowState.capture(win)); });
  win.on('closed', () => { win = null; });
}

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  Menu.setApplicationMenu(null);

  app.on('second-instance', () => {
    if (!win) return;
    if (win.isMinimized()) win.restore();
    win.focus();
  });

  ipcMain.handle('window-control', (_e, action) => {
    if (!win) return;
    if (action === 'minimize') win.minimize();
    else if (action === 'close') win.close();
  });

  app.whenReady().then(createWindow);
  app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
}
