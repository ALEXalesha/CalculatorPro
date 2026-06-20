# Calculators

Монорепозиторий с тремя калькуляторами. В репозиторий попадают **только исходники** —
скомпилированные бинарники, `node_modules`, `bin/`, `obj/`, `dist/` и инсталляторы
исключены через [`.gitignore`](.gitignore), поэтому репозиторий весит ~1.5 МБ вместо нескольких гигабайт.

У каждого калькулятора две независимые реализации одного и того же UI («liquid glass»):
**нативная** (WinUI 3 / WPF на .NET) и **Electron** (HTML + JS). Поэтому каждый калькулятор
разбит на отдельные подпроекты внутри своей папки.

## Структура

```
calculators/
├── calcpro/                 # Калькулятор №1 — только нативная версия (WPF)
│   ├── CalcPro.Wpf/         #   приложение на WPF (.NET 8)
│   ├── CalcPro.Tests/       #   модульные тесты движка вычислений
│   ├── installer/           #   скрипт Inno Setup
│   └── CalcPro.sln
│
├── calculator/              # Калькулятор №2 — две версии
│   ├── electron/            #   Electron-версия (main.js + calc-pro.html)
│   └── winui/               #   нативная версия (WinUI 3, .NET 8)
│
└── calculator-new/          # Калькулятор №3 — две версии (+ общие ассеты/скрипты упаковки)
    ├── electron/            #   Electron-версия (calculator-ios26)
    ├── winui/               #   нативная версия (CalcWinUI, .NET 9)
    ├── icon.svg             #   исходная иконка
    ├── calculator.html      #   автономный web-прототип
    └── *.nsi / *.py / *.cs  #   вспомогательные скрипты сборки/упаковки
```

## Сводка

| Калькулятор | Нативная версия | Electron-версия | Стек |
|---|---|---|---|
| **calcpro** | WPF, .NET 8 (`CalcPro.Wpf`) | — | C#, WPF-UI, CommunityToolkit.Mvvm |
| **calculator** | WinUI 3, .NET 8 (`winui/`) | да (`electron/`) | C# / HTML+JS, Electron 33 |
| **calculator-new** | WinUI 3, .NET 9 (`winui/`) | да (`electron/`) | C# / HTML+JS, Electron 33 |

> У `calcpro` Electron-варианта нет — это единственный калькулятор с одной (нативной) реализацией.

## Сборка

**Нативные (.NET) проекты** — нужен .NET SDK (8 или 9) и Windows:

```powershell
# пример для calcpro
dotnet build calcpro/CalcPro.sln -c Release
dotnet test  calcpro/CalcPro.Tests          # тесты только у calcpro

# WinUI-проекты
dotnet build calculator/winui/CalcPro.csproj -c Release
dotnet build calculator-new/winui/CalcWinUI.csproj -c Release
```

**Electron-проекты** — нужен Node.js:

```powershell
cd calculator/electron      # или calculator-new/electron
npm install                 # node_modules не в репозитории — ставится локально
npm start                   # запуск в dev
npm run build               # сборка портативного .exe (electron-builder)
```

Подробности по каждому проекту — в README/контекст-файлах внутри соответствующих папок.
