<div align="center">

# Calc Pro Glass

**Калькулятор в стиле Apple Liquid Glass с тремя режимами: обычный, инженерный и дроби «в столбик». Парсер без `eval`, движок проверен свойствами на fast-check.**

[Скачать для Windows](https://github.com/ALEXalesha/CalculatorPro/releases/latest) &nbsp;·&nbsp; [English version of this file](README.md) &nbsp;·&nbsp; [Все три калькулятора](../README.ru.md)

[![CI](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml/badge.svg)](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ALEXalesha/CalculatorPro?color=16a34a)](https://github.com/ALEXalesha/CalculatorPro/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

<img src="docs/screenshots/modes.png" width="900" alt="Три режима: Standard, Scientific и Fraction">

</div>

Калькулятор в стиле Apple Liquid Glass: скруглённое прозрачное окно без рамки,
градиентная подложка, стеклянные круглые кнопки. Три режима:

- **Standard** — выражения с приоритетами, процент в стиле Apple/Windows
  (`100 + 10 %` = 110, `100 × 10 %` = 10).
- **Scientific** — тригонометрия (+ обратные и гиперболические), `ln log eˣ 10ˣ`,
  корни `√ ³√ ʸ√x`, `x² x³ xʸ`, `n!`, `1/x`, `|x|`, `π e`, `EE`, DEG/RAD/GRAD, 2nd, память.
- **Fraction** — ввод дробей «в столбик» с курсором (`a/b`, стрелки, `Mix`, `Simp`),
  результат дробью или десятичной (`D⇄F`).

История (50 записей) и память хранятся в localStorage.

## Запуск и сборка

```powershell
npm ci
npm start              # запуск из исходников
npm test               # 127 unit- и property-тестов (node:test + fast-check)
npm run test:e2e       # e2e в настоящем Electron
npm run dist           # portable + NSIS-установщик в dist\
```

## Темы

Пять тем Paint Pro с его цветами: выбор кнопкой-палитрой рядом с «Историей», выбор запоминается.

<img src="docs/screenshots/themes.png" width="900" alt="Пять тем">

## Устройство

```
main.js          окно (frameless, transparent), single-instance, IPC свернуть/закрыть
preload.js       contextBridge → window.electronAPI.windowControl
src/index.html   разметка и CSS
src/calc-core.js движок без DOM (UMD: страница + Node-тесты)
src/app.js       отрисовка состояния, клики, клавиатура
build/icon.ico
test/            core.test.js (парсер, форматирование, дроби), calculator.test.js (автомат нажатий)
e2e/smoke.js     скрытое окно Electron, клики, проверки, скриншот
```

`calc-core.js` содержит парсер (shunting-yard, без `eval`), форматирование чисел и дробей
и `createCalculator()` — автомат нажатий. Страница подписывается на события
(`change`, `evaluated`, `history`, `altset`, `angle`, `mode`, `memflash`) и рисует `calc.state`.

Грамматика: `+ -` < `* /` < унарный минус < `^` (правоассоциативный). Неявное умножение
`2(3)`, `(1)(2)`, `2π`, `2sin(30)`. Незакрытые скобки закрываются на `=`.

## Хоткеи

`0–9 . ,` · `+ - * / ^ ( ) %` · `Enter` = · `Backspace` ⌫ · `Esc`/`Delete` AC ·
`Ctrl+H` история · `F1` Standard ↔ Scientific · в режиме дробей стрелки двигают курсор,
`/` открывает дробь.

## Безопасность

`contextIsolation`, `sandbox`, без `nodeIntegration`; Content-Security-Policy запрещает
любые скрипты, кроме своих файлов.

## Кадры для README собираются программой

`tools/make-screenshots.js` открывает настоящую страницу в скрытом окне того же
размера, что у приложения, в отдельной сессии в памяти (история человека в кадр
не попадает), нажимает кнопки кликами, как e2e-тест, и снимает кадр с самой
страницы через `capturePage()`.

```powershell
npx electron tools/make-screenshots.js
```

Первая версия скрипта снимала каждый кадр на одно нажатие позже: скрытое окно
Электрона перерисовывается с опозданием. Помогли `backgroundThrottling: false` и
ожидание двух кадров отрисовки перед снимком.

А сам кадр режима дробей нашёл ошибку в программе: строка над результатом и
запись в истории показывали `1/2 + 1/3` как `12 + 13`. Текст собирался
вырезанием тегов из дроби «в столбик», и черта пропадала. Теперь он строится из
самих дробей, а свойство на fast-check требует ровно одну черту на каждую дробь.

## Лицензия

MIT, файл [LICENSE](LICENSE).
