# calculator

Калькулятор №2 («Calc Pro»). Две независимые реализации одного UI.

## `electron/` — Electron-версия
Весь UI в одном файле `calc-pro.html`, обвязка Electron в `main.js` + `preload.js`.
- `npm install` → `npm start` — запуск в dev.
- `npm run build` — портативный `.exe`, `npm run build-installer` — NSIS-инсталлятор (electron-builder).
- `DEV_CONTEXT.md` — заметки по разработке.

## `winui/` — нативная версия
WinUI 3, .NET 8 (`net8.0-windows10.0.19041.0`), self-contained.
Логика калькулятора в `Calculator/` (`Engine.cs`, `Format.cs`, `Fractions.cs`, `Input.cs`,
`State.cs`, `HistoryStore.cs`), UI в `MainWindow.xaml`.
- `dotnet build winui/CalcPro.csproj -c Release`
- `installer.nsi` — скрипт NSIS, `README.md` внутри `winui/` — детали по нативной сборке.

Артефакты сборки (`bin/`, `obj/`, `dist/`, `node_modules/`, `publish-final/` и т.п.) в репозиторий не входят.
