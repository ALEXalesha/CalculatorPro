# Calc Pro Glass (Electron)

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
npm test               # 111 unit/property-тестов (node:test + fast-check)
npm run test:e2e       # e2e в настоящем Electron
..\build.ps1 -Only glass   # portable + NSIS-установщик в ..\dist
```

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
