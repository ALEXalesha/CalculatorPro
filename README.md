# Calculators

Три настольных калькулятора для Windows в стиле Apple Liquid Glass. У каждого есть
portable-версия (один `.exe` без установки) и установщик.

| Папка | Приложение | Стек | Что умеет |
|---|---|---|---|
| [`calcpro-wpf/`](calcpro-wpf/README.md) | **Calc Pro** | C# 12, WPF, .NET 8 | Выражения с приоритетами на `decimal` (0.1 + 0.2 = 0.3 точно), скобки, научные функции, память, история, undo/redo |
| [`calcpro-glass/`](calcpro-glass/README.md) | **Calc Pro Glass** | Electron 43, HTML/CSS/JS | Режимы Standard / Scientific / Fraction (ввод дробей «в столбик»), DEG/RAD/GRAD, 2nd, память, история |
| [`ios-calculator/`](ios-calculator/README.md) | **Calculator iOS 26** | Electron 43, HTML/CSS/JS | Копия калькулятора iPhone: пошаговое вычисление, научная панель, история |

## Быстрый старт

Нужны .NET SDK 8+, Node.js 20+ и Inno Setup 6 (для установщика Calc Pro).

```powershell
.\build.ps1                 # тесты + portable + setup для всех трёх → .\dist
.\build.ps1 -Only glass     # только одно приложение: wpf | glass | ios
.\build.ps1 -SkipTests      # только упаковка
```

Результат в `dist/`:

```
CalcPro-1.1.0-Portable.exe          CalcPro-1.1.0-Setup.exe
CalcProGlass-1.1.0-Portable.exe     CalcProGlass-1.1.0-Setup.exe
CalculatorIOS26-1.1.0-Portable.exe  CalculatorIOS26-1.1.0-Setup.exe
```

Запуск из исходников:

```powershell
dotnet run --project calcpro-wpf\src\CalcPro.Wpf
cd calcpro-glass;  npm ci; npm start
cd ios-calculator; npm ci; npm start
```

## Тесты

```powershell
dotnet test calcpro-wpf\CalcPro.sln          # 293 теста (xUnit + FsCheck)
cd calcpro-glass;  npm test; npm run test:e2e # 111 unit + e2e в Electron
cd ios-calculator; npm test; npm run test:e2e # 45 unit + e2e в Electron
```

Основа — property-based тесты: генератор выдаёт тысячи случайных выражений и
последовательностей нажатий, а тест проверяет законы и инварианты. Подробно в
[docs/TESTING.md](docs/TESTING.md).

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
│   └── installer/         Inno Setup
├── calcpro-glass/         Calc Pro Glass (Electron)
│   ├── src/calc-core.js   движок без DOM
│   ├── src/app.js         отрисовка и события
│   ├── test/  e2e/
└── ios-calculator/        Calculator iOS 26 (Electron)
    ├── src/calc-engine.js движок без DOM
    ├── src/app.js
    ├── test/  e2e/
```

В репозитории только исходники. `bin/`, `obj/`, `node_modules/` и `dist/` игнорируются.
Прежние версии (WinUI-порты, архивы, прототипы) остались в истории git, в коммите `3c8fb43`.
