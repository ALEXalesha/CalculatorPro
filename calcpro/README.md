# calcpro

Калькулятор №1. Единственный из трёх — **только нативная версия** (Electron-варианта нет).

- **`CalcPro.Wpf/`** — приложение на WPF, .NET 8 (`net8.0-windows`).
  Зависимости: `WPF-UI`, `CommunityToolkit.Mvvm`. Архитектура MVVM, движок вычислений
  (токенизатор → парсер → вычислитель) в `Services/`, команды калькулятора в `Commands/`.
- **`CalcPro.Tests/`** — модульные тесты движка (токенизатор, парсер, вычислитель, история).
- **`installer/CalcPro.iss`** — скрипт Inno Setup для инсталлятора.
- **`README_original.md`** — исходный подробный README проекта.

## Сборка

```powershell
dotnet build CalcPro.sln -c Release
dotnet test CalcPro.Tests
```

Сборочные артефакты (`bin/`, `obj/`, `dist/`, готовый `CalcPro.exe`) в репозиторий не входят.
