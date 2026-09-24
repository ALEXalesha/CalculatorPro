# Calculator iOS 26 (Electron)

[Download for Windows](https://github.com/ALEXalesha/CalculatorPro/releases/latest) &nbsp;·&nbsp; [Русская версия этого файла](README.ru.md) &nbsp;·&nbsp; [All three calculators](../README.md)

<img src="docs/screenshots/modes.png" width="760" alt="Calculator iOS 26: basic, scientific panel and history">

A copy of the Calculator app from iOS 26 in the Liquid Glass style: a coloured background under glass and round buttons that stay round at any window size. The interface is in Russian: «Научные функции» opens the scientific panel, «История» is history, «Очистить» clears it.

## Behaviour: two lines, as on the iPhone

- The white line at the bottom grows as you type: `2`, `2 + `, `2 + 3`.
- The grey line above fills on `=` or when operations are chained.
- Evaluation is step by step, without precedence: `2 + 3 × 4 =` gives 20 (after `×` it shows `5 × `).
- `C` clears the current number first; a second press (`AC`) clears everything.
- `100 + 20 %` gives 120; `±` after an operator starts a negative number.
- Numbers are written the Russian way: a space between thousands and a decimal comma, `1 234,56`.
- The scientific panel (the `•••` menu or the «Научные функции» bar): `sinh cosh`, memory, `2nd`, powers, roots, `ln log₁₀ x!`, trigonometry, `Rad`, `π e Rand`.
- History keeps 200 entries; clicking one continues the calculation with its result.

## Themes

The five themes of Paint Pro under «Тема» in the `•••` menu; the choice is remembered. Operators take the accent of the theme instead of the iPhone orange, as Paint's buttons do.

<img src="docs/screenshots/themes.png" width="900" alt="The five themes">

## Running and building

```powershell
npm ci
npm start              # F12 opens DevTools, only when run from sources
npm test               # 78 unit and property tests
npm run test:e2e       # the real page in Electron
npx electron tools/make-screenshots.js   # the README frames
..\build.ps1 -Only ios # portable exe and NSIS installer into ..\dist
```

## Layout

```
main.js             320×640 window (minimum 280×500), frameless, transparent; IPC for minimise/close;
                    opens where it was closed (window-state.js, the same file as in Calc Pro Glass)
preload.js          contextBridge → window.windowAPI
src/index.html      markup and CSS
src/calc-engine.js  engine without DOM (UMD)
src/app.js          button layout (fit), rendering, clicks, keyboard, history
test/engine.test.js
e2e/smoke.js
tools/make-screenshots.js
```

`fit()` in `app.js` sizes the buttons: the columns always take the full width, and a row is the smaller of the width-based and the height-based size, so keys are round when the height allows and stretch into capsules when it does not, as on an iPhone in landscape. In a low window (the minimum is 280×500, below the 320×500 of the Windows calculator) the display gives up height first. Long numbers in the history wrap instead of sliding out of sight.

## Tests

A regression set from the old inline script plus cases for every fixed bug. A chain of operations folds left to right like on the iPhone (a model against the engine, 2000 chains). `formatNumber` and `parseDisplay` are an inverse pair. 3000 random key sequences check that there is no `NaN` or `Infinity`, the result shown is always a number, memory stays finite and history stays within 200. The end-to-end run clicks through the page in a hidden Electron window and checks that the page has no access to Node.js.

When the logic was moved out of the HTML into modules, the old inline script ran in `vm` with DOM stubs side by side with the new module on 3000 random sequences, comparing state after every press. Only once both matched completely were the fixes from the changelog made.

## Keyboard

`0–9 . ,` · `+ - * /` · `Enter` = · `Backspace` · `Esc` AC · `%` · `Esc` closes history.

## Security

The page used to run with `nodeIntegration: true` and `contextIsolation: false`, that is, with full access to Node.js. Now it has `contextIsolation`, `sandbox` and a Content-Security-Policy, and it talks to the window only through `preload.js`.
