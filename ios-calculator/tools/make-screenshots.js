// Кадры для README: собираются программой, а не снимком экрана.
//
//   npx electron tools/make-screenshots.js
//
// Настоящая страница в скрытом окне с тем же preload, что у приложения, и в
// отдельной сессии в памяти: история из localStorage человека в кадр не попадёт.
// Нажатия - настоящие клики по кнопкам, как в e2e/smoke.js. Кадр берётся с самой
// страницы (capturePage), поэтому чужое окно в него попасть не может. Три кадра
// склеиваются в одну картинку рядом.
const { app, BrowserWindow, nativeImage } = require('electron');
const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const OUT = path.join(ROOT, 'docs', 'screenshots');
const W = 360;
const H = 720;
const GAP = 24;

app.whenReady().then(async () => {
  setTimeout(() => { console.error('timeout'); app.exit(2); }, 60000);
  const win = new BrowserWindow({
    width: W, height: H, show: false, frame: false, transparent: true,
    webPreferences: {
      preload: path.join(ROOT, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: true,
      partition: `shots-${process.pid}-${Date.now()}`,
      // Скрытое окно иначе рисует с опозданием, и capturePage отдаёт прошлый кадр.
      backgroundThrottling: false,
    },
  });
  await win.loadFile(path.join(ROOT, 'src', 'index.html'));

  const js = (code) => win.webContents.executeJavaScript(code);
  const click = (sel) => js(`(() => { const el = document.querySelector(${JSON.stringify(sel)}); if (!el) throw new Error('no ' + ${JSON.stringify(sel)}); el.click(); })()`);
  const num = async (s) => { for (const d of s) await click(`.btn[data-num="${d}"]`); };
  const op = (o) => click(`.btn[data-op="${o}"]`);
  const act = (a) => click(`.btn[data-act="${a}"]`);
  const frame = async (name) => {
    await new Promise((r) => setTimeout(r, 500)); // подсветка нажатия и выезд панели гаснут
    win.webContents.invalidate();
    await js('new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))');
    const image = await win.webContents.capturePage();
    fs.writeFileSync(path.join(OUT, name), image.toPNG());
    console.log(`  ${name}: ${await js('document.getElementById("result").textContent')}`);
    return image;
  };

  fs.mkdirSync(OUT, { recursive: true });

  // Пошаговое вычисление, как на iPhone: 2 + 3 × 4 = 20, числа по-русски.
  await num('2'); await op('+'); await num('3'); await op('*'); await num('4'); await op('=');
  await act('clear'); await act('clear');
  await num('1234.5'); await op('*'); await num('12'); await op('=');
  const basic = await frame('basic.png');

  await click('#sciToggle');
  await act('clear'); await act('clear');
  await num('2500'); await act('sqrt');
  const scientific = await frame('scientific.png');

  await click('#menuBtn');
  await click('.menu-item[data-action="history"]');
  const history = await frame('history.png');

  const scale = basic.getSize().width / W;
  const join = (images, name) => {
    const w = Math.round((W * images.length + GAP * (images.length - 1)) * scale);
    const h = Math.round(H * scale);
    const canvas = Buffer.alloc(w * h * 4);
    images.forEach((img, i) => {
      const bmp = img.toBitmap();
      const { width, height } = img.getSize();
      const x0 = Math.round(i * (W + GAP) * scale);
      for (let y = 0; y < height; y++) {
        bmp.copy(canvas, ((y * w) + x0) * 4, y * width * 4, (y + 1) * width * 4);
      }
    });
    fs.writeFileSync(path.join(OUT, name),
      nativeImage.createFromBitmap(canvas, { width: w, height: h, scaleFactor: scale }).toPNG());
    console.log(`  ${name}`);
  };
  join([basic, scientific, history], 'modes.png');

  // Пять тем Paint Pro на одном примере; тема ставится тем же CalcTheme.apply, что из меню.
  await click('#historyBack');
  // Переходы цветов выключаются: смена темы плавная (0.2-0.3 с), и кадр, снятый
  // сразу, ловил середину перехода - в «Светлой» цифры выходили серыми.
  await js(`document.head.appendChild(Object.assign(document.createElement('style'), { textContent: '*, *::before, *::after { transition: none !important; animation: none !important; }' }))`);
  const themed = [];
  for (const id of ['glass', 'formal', 'light', 'night', 'warm']) {
    await js(`CalcTheme.apply(document.documentElement, ${JSON.stringify(id)}, null)`);
    themed.push(await frame(`theme-${id}.png`));
  }
  join(themed, 'themes.png');
  for (const id of ['glass', 'formal', 'light', 'night', 'warm']) fs.unlinkSync(path.join(OUT, `theme-${id}.png`));
  app.exit(0);
}).catch((e) => { console.error(e); app.exit(1); });
