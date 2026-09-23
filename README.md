<div align="center">

# Calculators

**Three desktop calculators for Windows in the Apple Liquid Glass style: one in C# and WPF, two in Electron. Each has its own expression engine with no `eval`, and 491 tests, most of them properties over random input.**

[Download for Windows](https://github.com/ALEXalesha/CalculatorPro/releases/latest) &nbsp;·&nbsp; [Русская версия этого файла](README.ru.md)

[![CI](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml/badge.svg)](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ALEXalesha/CalculatorPro?color=16a34a)](https://github.com/ALEXalesha/CalculatorPro/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

Every release has a portable `.exe` (no installation) and an installer for each of the three.

## Calc Pro: C# 12, WPF, .NET 8

<img src="calcpro-wpf/docs/screenshots/standard.png" width="760" alt="Calc Pro: exact decimal arithmetic with history">

Expressions with operator precedence, computed in `decimal`, so `0.1 + 0.2` is exactly `0.3`. Brackets with implicit multiplication, scientific functions, memory, history, and undo/redo for every key press. The engine (`CalcPro.Core`) knows nothing about WPF: tokenizer, Pratt parser, evaluator, and a command per key press. [More](calcpro-wpf/README.md)

## Calc Pro Glass: Electron

<img src="calcpro-glass/docs/screenshots/modes.png" width="760" alt="Calc Pro Glass: Standard, Scientific and Fraction modes">

Three modes: Standard, Scientific with DEG/RAD/GRAD and 2nd, and Fraction, where fractions are typed "in a column" with a cursor. The parser is shunting-yard. [More](calcpro-glass/README.md)

## Calculator iOS 26: Electron

<img src="ios-calculator/docs/screenshots/modes.png" width="760" alt="Calculator iOS 26: basic, scientific panel and history">

A copy of the iPhone calculator: step-by-step evaluation without precedence (`2 + 3 × 4 =` gives 20, as on the phone), round buttons that stay round at any window size, a scientific panel, and history. The interface is in Russian, numbers are written the Russian way: `1 234,5`. [More](ios-calculator/README.md)

## Themes

All three have the same five themes as Paint Pro, with the same colours: Стеклянная (glass, the default), Строгая (formal), Светлая (light), Ночная (night) and Тёплая (warm). The calculators and Paint were meant to look like one family, and now they do. The theme is picked from the palette button (Calc Pro, Calc Pro Glass) or the `•••` menu (Calculator iOS 26), changes the window at once and is remembered between runs.

<img src="calcpro-glass/docs/screenshots/themes.png" width="900" alt="The five themes of Paint Pro in Calc Pro Glass">

## Tests

```powershell
dotnet test calcpro-wpf\CalcPro.sln          # 310 (xUnit + FsCheck)
cd calcpro-glass;  npm test; npm run test:e2e # 127 + e2e in Electron
cd ios-calculator; npm test; npm run test:e2e # 54 + e2e in Electron
```

Most tests state a law instead of an example: a random expression tree printed with minimal brackets parses back into the same tree; `sin² + cos² = 1`; undoing every key press returns the initial state. The key-press state machines get thousands of random sequences, and after every single press the display, the expression and the memory are checked. [docs/TESTING.md](docs/TESTING.md) lists them all (in Russian).

These properties keep finding real bugs. Getting this repository ready for publication, the random-keys property of Calc Pro Glass found that `EE` right after `=` glued an `E` onto a stale display (`0E`) which the next digit then wiped. The README screenshots found two more in Calc Pro: the expression line showed `*` and `/` while the buttons say `×` and `÷`, and the memory badge stretched into a tall bar.

The theme tests read the theme files themselves: all five must define the same keys, text must keep at least 4.5:1 on its keys in every theme, and the markup may reach theme colours only through the theme. They found that the AC, ± and % keys were below that contrast in two themes, 3.37:1 in glass and 2.60:1 in formal, and that was fixed before the release.

CI runs the C# and JavaScript engine tests on every push. The end-to-end runs need a real Electron window and are run locally before a release.

## Building

.NET SDK 8+, Node.js 20+ and Inno Setup 6 (for the Calc Pro installer):

```powershell
.\build.ps1                 # tests, portable and setup for all three into .\dist
.\build.ps1 -Only glass     # one app: wpf | glass | ios
.\build.ps1 -SkipTests      # packaging only
```

From sources:

```powershell
dotnet run --project calcpro-wpf\src\CalcPro.Wpf
cd calcpro-glass;  npm ci; npm start
cd ios-calculator; npm ci; npm start
```

## Screenshots are generated

No screenshot here was taken from the screen. `calcpro-wpf/tools/CalcPro.Screenshots` opens the real `MainWindow` far off screen, presses keys through the view model's commands and renders the window itself with `RenderTargetBitmap`. The two Electron apps have `tools/make-screenshots.js`: the real page in a hidden window with an in-memory session, real clicks, and `capturePage()`. No other window can get into the frame.

## Layout

```
build.ps1            everything into dist/
docs/TESTING.md      what the tests check
calcpro-wpf/         Calc Pro: CalcPro.Core (engine), CalcPro.Wpf (window), tests, installer
calcpro-glass/       Calc Pro Glass: src/calc-core.js (engine without DOM), src/app.js, test, e2e
ios-calculator/      Calculator iOS 26: src/calc-engine.js (engine without DOM), src/app.js, test, e2e
```

## Licence

MIT, see [LICENSE](LICENSE).
