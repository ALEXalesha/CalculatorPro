// Кадры для README: собираются программой, а не снимком экрана.
//
//   npx electron tools/make-screenshots.js
//
// Настоящая страница в скрытом окне того же размера, что у приложения (main.js),
// с тем же preload и в отдельной сессии в памяти, чтобы в кадр не попала история
// из localStorage человека. Нажатия - настоящие клики по кнопкам, как в
// e2e/smoke.js. Кадр берётся с самой страницы (capturePage), поэтому чужое окно
// в него попасть не может. Три режима склеиваются в одну картинку рядом.
const { app, BrowserWindow, nativeImage } = require('electron');
const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const OUT = path.join(ROOT, 'docs', 'screenshots');
const W = 320;
const H = 640;
const GAP = 24;

app.whenReady().then(async () => {
  setTimeout(() => { console.error('timeout'); app.exit(2); }, 30000);
  const win = new BrowserWindow({
    width: W, height: H, show: false, frame: false, transparent: true,
    webPreferences: {
      preload: path.join(ROOT, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: true,
      partition: `shots-${process.pid}-${Date.now()}`,
      // Скрытое окно иначе рисует с опозданием, и capturePage отдаёт прошлый
      // кадр: в первой версии скрипта каждый снимок отставал на одно нажатие.
      backgroundThrottling: false,
    },
  });
  await win.loadFile(path.join(ROOT, 'src', 'index.html'));

  const js = (code) => win.webContents.executeJavaScript(code);
  const click = (sel) => js(`(() => { const el = document.querySelector(${JSON.stringify(sel)}); if (!el) throw new Error('no ' + ${JSON.stringify(sel)}); el.click(); })()`);
  const keys = async (list) => { for (const k of list) await click(`[data-key="${k}"]`); };
  const settle = () => new Promise((r) => setTimeout(r, 450)); // рябь от нажатия гаснет
  const frame = async (name) => {
    await settle();
    win.webContents.invalidate();
    await js('new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))');
    const image = await win.webContents.capturePage();
    fs.writeFileSync(path.join(OUT, name), image.toPNG());
    console.log(`  ${name}`);
    return image;
  };

  fs.mkdirSync(OUT, { recursive: true });

  // Standard: процент в стиле Apple/Windows, 100 + 10 % = 110.
  await keys(['1', '0', '0', '+', '1', '0', '%', '=']);
  const standard = await frame('standard.png');

  await click('#tabScientific');
  await keys(['AC', 'sin', '3', '0', ')', '+', '√', '2', '=']);
  const scientific = await frame('scientific.png');

  await click('#tabFraction');
  await keys(['AC', 'a/b', '1', 'a/b', '2', '+', 'a/b', '1', 'a/b', '3', '=']);
  const fraction = await frame('fraction.png');

  // Три кадра рядом: холст с прозрачным фоном, окна с отступом.
  const scale = standard.getSize().width / W;
  const w = Math.round((W * 3 + GAP * 2) * scale);
  const h = Math.round(H * scale);
  const canvas = Buffer.alloc(w * h * 4);
  [standard, scientific, fraction].forEach((img, i) => {
    const bmp = img.toBitmap();
    const { width, height } = img.getSize();
    const x0 = Math.round(i * (W + GAP) * scale);
    for (let y = 0; y < height; y++) {
      bmp.copy(canvas, ((y * w) + x0) * 4, y * width * 4, (y + 1) * width * 4);
    }
  });
  const joined = nativeImage.createFromBitmap(canvas, { width: w, height: h, scaleFactor: scale });
  fs.writeFileSync(path.join(OUT, 'modes.png'), joined.toPNG());
  console.log('  modes.png');

  const shown = await js('document.querySelector("#mainResult").textContent.trim()');
  console.log(`дробь в кадре: ${shown}`);
  app.exit(0);
}).catch((e) => { console.error(e); app.exit(1); });
