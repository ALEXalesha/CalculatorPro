# calculator-new

Калькулятор №3 («Calculator iOS 26», Liquid Glass). Две независимые реализации + общие ассеты.

## `electron/` — Electron-версия (`calculator-ios26`)
UI в `renderer/index.html`, обвязка в `main.js` + `preload.js`, иконки в `assets/`.
- `npm install` → `npm start` — запуск.
- `npm run build` — NSIS + портативная сборка (electron-builder, вывод в `build/` — игнорируется).
- `npm test` — `node test-logic.js`; есть также `test-buttons.js`, `test-real-click.ps1`.

## `winui/` — нативная версия (`CalcWinUI`)
WinUI 3, .NET 9 (`net9.0-windows10.0.26100.0`). Движок в `CalculatorEngine.cs`,
UI в `MainPage.xaml` / `MainWindow.xaml`. Профили публикации (x64/x86/arm64) в `Properties/PublishProfiles/`.
- `dotnet build winui/CalcWinUI.csproj -c Release`
- `AGENTS.md` и `.github/instructions/` — гайдлайны по коду.

## Общие файлы в корне папки
- `calculator.html` — автономный web-прототип UI.
- `icon.svg` — исходная векторная иконка.
- `calcwinui-setup.nsi` — скрипт NSIS для нативной версии.
- `7za_wrapper.cs`, `extract_wincodesign.py` — вспомогательные скрипты упаковки.
- `CONTEXT.md`, `CLAUDE_CODE_PROMPT.md` — контекст и история разработки.

Артефакты сборки и большие бинарники (`dist/`, `node_modules/`, `bin/`, `obj/`, `calc-payload.7z`,
`files.zip`, готовые `.exe`) в репозиторий не входят.
