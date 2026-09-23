<div align="center">

# Calculators

**Три настольных калькулятора для Windows в стиле Apple Liquid Glass: один на C# и WPF, два на Electron. У каждого свой движок выражений без `eval` и вместе 461 тест, большая часть — свойства на случайных входах.**

[Скачать для Windows](https://github.com/ALEXalesha/CalculatorPro/releases/latest) &nbsp;·&nbsp; [English version of this file](README.md)

[![CI](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml/badge.svg)](https://github.com/ALEXalesha/CalculatorPro/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ALEXalesha/CalculatorPro?color=16a34a)](https://github.com/ALEXalesha/CalculatorPro/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

В каждом релизе у всех трёх есть portable-версия (один `.exe` без установки) и установщик.

| Папка | Приложение | Стек | Что умеет |
|---|---|---|---|
| [`calcpro-wpf/`](calcpro-wpf/README.ru.md) | **Calc Pro** | C# 12, WPF, .NET 8 | Выражения с приоритетами на `decimal` (0.1 + 0.2 = 0.3 точно), скобки, научные функции, память, история, undo/redo |
| [`calcpro-glass/`](calcpro-glass/README.ru.md) | **Calc Pro Glass** | Electron, HTML/CSS/JS | Режимы Standard / Scientific / Fraction (ввод дробей «в столбик»), DEG/RAD/GRAD, 2nd, память, история |
| [`ios-calculator/`](ios-calculator/README.ru.md) | **Calculator iOS 26** | Electron, HTML/CSS/JS | Копия калькулятора iPhone: пошаговое вычисление, научная панель, история |

<img src="calcpro-wpf/docs/screenshots/standard.png" width="760" alt="Calc Pro">

<img src="calcpro-glass/docs/screenshots/modes.png" width="760" alt="Calc Pro Glass">

<img src="ios-calculator/docs/screenshots/modes.png" width="760" alt="Calculator iOS 26">

## Быстрый старт

Нужны .NET SDK 8+, Node.js 20+ и Inno Setup 6 (для установщика Calc Pro).

```powershell
.\build.ps1                 # тесты + portable + setup для всех трёх → .\dist
.\build.ps1 -Only glass     # только одно приложение: wpf | glass | ios
.\build.ps1 -SkipTests      # только упаковка
```

Результат в `dist/`:

```
CalcPro-1.2.0-Portable.exe          CalcPro-1.2.0-Setup.exe
CalcProGlass-1.2.0-Portable.exe     CalcProGlass-1.2.0-Setup.exe
CalculatorIOS26-1.2.0-Portable.exe  CalculatorIOS26-1.2.0-Setup.exe
```

Запуск из исходников:

```powershell
dotnet run --project calcpro-wpf\src\CalcPro.Wpf
cd calcpro-glass;  npm ci; npm start
cd ios-calculator; npm ci; npm start
```

## Тесты

```powershell
dotnet test calcpro-wpf\CalcPro.sln          # 298 тестов (xUnit + FsCheck)
cd calcpro-glass;  npm test; npm run test:e2e # 118 unit + e2e в Electron
cd ios-calculator; npm test; npm run test:e2e # 45 unit + e2e в Electron
```

Основа — property-based тесты: генератор выдаёт тысячи случайных выражений и
последовательностей нажатий, а тест проверяет законы и инварианты. Подробно в
[docs/TESTING.md](docs/TESTING.md).

Свойства продолжают находить настоящие баги. При подготовке к публикации свойство со
случайными нажатиями в Calc Pro Glass нашло, что `EE` сразу после `=` приклеивал `E`
к устаревшему экрану (`0E`), а следующая цифра всё стирала. Кадры для README нашли ещё
два в Calc Pro: строка выражения показывала `*` и `/`, хотя на кнопках `×` и `÷`, а
значок памяти растягивался в высокую полоску.

CI на каждый пуш гоняет тесты движков на C# и JavaScript. e2e требуют настоящего окна
Electron и запускаются локально перед выпуском.

## Кадры собирает программа

Ни один снимок здесь не снят с экрана. `calcpro-wpf/tools/CalcPro.Screenshots`
открывает настоящее `MainWindow` далеко за краем экрана, нажимает кнопки через команды
ViewModel и рисует окно само через `RenderTargetBitmap`. У обоих Electron-приложений
`tools/make-screenshots.js`: настоящая страница в скрытом окне с сессией в памяти,
настоящие клики и `capturePage()`. Чужое окно в кадр попасть не может.

## Структура

```
calculators/
├── build.ps1              сборка всего в dist/
├── docs/TESTING.md        что и как проверяют тесты
├── CHANGELOG.md
├── calcpro-wpf/           Calc Pro (WPF)
│   ├── src/CalcPro.Core/  движок без UI: токенизатор, Pratt-парсер, decimal-вычислитель, команды с undo
│   ├── src/CalcPro.Wpf/   окно, MVVM, стили
│   ├── tests/             xUnit + FsCheck
│   ├── tools/             генератор кадров для README
│   └── installer/         Inno Setup
├── calcpro-glass/         Calc Pro Glass (Electron)
│   ├── src/calc-core.js   движок без DOM
│   ├── src/app.js         отрисовка и события
│   ├── test/  e2e/  tools/
└── ios-calculator/        Calculator iOS 26 (Electron)
    ├── src/calc-engine.js движок без DOM
    ├── src/app.js
    ├── test/  e2e/  tools/
```

В репозитории только исходники. `bin/`, `obj/`, `node_modules/` и `dist/` игнорируются.

## Лицензия

MIT, см. [LICENSE](LICENSE).
