# Calc Pro

Windows-калькулятор с GUI в стиле Apple Liquid Glass (iOS 26 / macOS Tahoe 26).
C# 12 + WPF на .NET 8, чистая MVVM-архитектура.

## Чем интересен

- **Своё ядро вычислений.** Tokenizer → Pratt-parser → AST → Evaluator. Никакого
  `eval`, никакого `DataTable.Compute`. Полный контроль над приоритетами и безопасностью.
- **Точные вычисления.** `decimal` везде, где можно (округление к 12 знакам через
  `MidpointRounding.ToEven`). `0.1 + 0.2 == 0.3` буквально. К `double` переключаемся
  только в `sin/cos/log/exp` — единственное место, изолировано в `EvalTranscendental`.
- **Command Pattern для undo/redo.** Каждое нажатие — это объект `ICalcCommand`
  c `Execute()`/`Undo()`. Стек хранит команды, не snapshot'ы строки.
- **Explicit state-machine.** `CalcPhase` enum: `Idle | EnteringDigit | AfterOperator
  | AfterEquals`. Никаких разбросанных `justEvaluated`, `nextClearsDisplay`.
- **MVVM без code-behind на UI-стейт.** View-биндинги через `{Binding}`,
  ViewModel — единственное место, мутирующее `CalcState`. Code-behind содержит
  только декоративные анимации (пузыри, pop-glow на `=`, ширина scientific-колонок).

## Запуск

Требуется .NET 8 SDK ([скачать](https://dotnet.microsoft.com/download/dotnet/8.0)).

```bash
cd CalcPro
dotnet restore
dotnet run --project CalcPro.Wpf
```

Тесты:

```bash
dotnet test
```

Single-file релизный .exe (~80–150 МБ):

```bash
dotnet publish CalcPro.Wpf -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true
```

С тримом (~15 МБ, но WPF + reflection могут спорить, проверяй):

```bash
dotnet publish CalcPro.Wpf -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Архитектура

```
CalcPro.sln
├── CalcPro.Wpf/                # main app
│   ├── Models/                 # enums + plain state types
│   │   ├── CalcMode.cs         # Standard | Scientific
│   │   ├── CalcPhase.cs        # state-machine enum
│   │   ├── AngleMode.cs        # Deg | Rad
│   │   ├── HistoryEntry.cs     # immutable record { Expression, Result, Timestamp }
│   │   └── CalcState.cs        # current state object owned by ViewModel
│   ├── Services/               # pure logic, no UI knowledge
│   │   ├── Token.cs            # token kind enum + readonly record struct
│   │   ├── Tokenizer.cs        # lexer (string → tokens)
│   │   ├── Ast.cs              # AST node records
│   │   ├── Parser.cs           # Pratt parser (tokens → AST)
│   │   ├── Evaluator.cs        # AST → decimal
│   │   └── HistoryManager.cs   # history list (entries) + undo/redo stacks
│   ├── Commands/               # one Command per user action
│   │   ├── ICalcCommand.cs     # interface { Execute, Undo, Description }
│   │   ├── BaseCalcCommand.cs  # captures previous state for Undo
│   │   ├── DigitCommand.cs
│   │   ├── OperatorCommand.cs
│   │   ├── ParenCommand.cs
│   │   ├── EqualsCommand.cs
│   │   ├── ClearCommand.cs     # AC + C
│   │   ├── BackspaceCommand.cs
│   │   ├── SignCommand.cs      # ±
│   │   ├── FunctionCommand.cs  # sin/cos/sqrt/...
│   │   └── MemoryCommand.cs    # M+/M-/MS/MR/MC
│   ├── ViewModels/
│   │   └── CalcViewModel.cs    # all WPF-bound state + ICommand exposure
│   ├── Converters/
│   │   └── BoolToVisConverter.cs
│   ├── Resources/Brushes.xaml  # colors, gradients, bubble fills
│   ├── Styles/
│   │   ├── Theme.xaml          # fonts, window background gradients
│   │   ├── GlassPanel.xaml     # .panel ControlTemplate
│   │   └── GlassButton.xaml    # 5 button variants
│   ├── App.xaml(.cs)
│   └── MainWindow.xaml(.cs)    # layout + bubble decoration + display animations
└── CalcPro.Tests/              # xUnit
    ├── TokenizerTests.cs
    ├── ParserTests.cs
    ├── EvaluatorTests.cs
    └── HistoryManagerTests.cs
```

## Хоткеи

| Клавиша                     | Действие                                  |
| --------------------------- | ----------------------------------------- |
| `0`–`9`, `.`, `,`           | Цифры (запятая трактуется как точка)      |
| `+` `-` `*` `/`             | Операции                                  |
| `Enter`, `=`                | Вычислить                                 |
| `Backspace`                 | Удалить последний символ                  |
| `Delete`                    | Сбросить текущее число (C)                |
| `Escape`                    | Полный сброс (AC)                         |
| `^`                         | Степень                                   |
| `%`                         | Процент                                   |
| `Ctrl+Z` / `Ctrl+Y`         | Undo / Redo                               |
| `Ctrl+C` / `Ctrl+V`         | Копировать результат / вставить число     |
| `Ctrl+H`                    | Скрыть/показать панель истории            |
| `Ctrl+M`                    | Memory Recall                             |

Скобки `(` / `)` сейчас только через клики; добавить хоткеи — отдельная строка
в `Window.InputBindings`.

## Как добавить Programmer mode (HEX/BIN/OCT)

Архитектура к этому готова, никакого рефакторинга не требуется:

1. Добавить значение `Programmer` в `Models/CalcMode.cs`.
2. Добавить `NumberBase` enum (`Dec | Hex | Bin | Oct`) в `Models/`.
3. В `Services/Tokenizer.cs` распознавать `0x`-, `0b`-, `0o`-префиксы.
4. В `Services/Evaluator.cs` поддержать побитовые операции (новые `BinaryOpNode`
   с операторами `&`, `|`, `xor`, `~`, `<<`, `>>`).
5. В `ViewModels/CalcViewModel.cs` добавить `[ObservableProperty] private NumberBase _base`.
6. В `MainWindow.xaml` развернуть третий слой колонок (как сделана scientific-колонка)
   с кнопками `AND`, `OR`, `XOR`, `NOT`, `<<`, `>>` и переключатель base.

Все изменения локальны — благодаря разделению Tokenizer/Parser/Evaluator
и MVVM-связке.

## Визуальный язык

- **Фон**: четыре `RadialGradientBrush` (фиолетовый, розовый, голубой, зелёный)
  поверх линейного градиента `#0E0C1A → #1A1330 → #0C1024`.
- **Пузыри**: 10 `Ellipse` в `Canvas` с собственными `Storyboard`. Каждый
  всплывает с разной длительностью и дрейфом, циклично, в `RepeatBehavior.Forever`.
- **Стеклянные панели**: `ContentControl` с `GlassPanel` стилем — двойной `Border`
  (фон + specular блик сверху) + `ContentPresenter` + `DropShadowEffect`.
- **Кнопки**: `Button` с `ControlTemplate`, `ScaleTransform` на pressed (0.97),
  `TranslateTransform` Y на hover (-1px), все переходы через Storyboard.
- **Результат `=`**: pop-анимация — display Y 18 → 0 + `DropShadowEffect`
  BlurRadius 30 → 0 за 380 мс.

Дизайн-токены — те же, что в Paint Pro Electron (см. `paint-pro-electron/DEV_CONTEXT.md`).

## Что внутри парсера

Pratt-парсер с приоритетами (lbp = left binding power):

| Токен     | LBP | Замечания                          |
| --------- | --- | ---------------------------------- |
| `+` `-`   | 70  | Левая ассоциативность              |
| `*` `/`   | 80  | Левая ассоциативность              |
| `%` `!`   | 95  | Постфикс                           |
| `^`       | 100 | **Правая ассоциативность**         |
| `(`       | 120 | Группировка / вызов функции        |
| унарный `-` `+` | rbp=90 | nud-handler                |

Тесты в `ParserTests.cs` фиксируют эти правила. Самое тонкое — правая
ассоциативность `^`: `2^2^3 == 2^(2^3) == 256`, парсится через `Expression(99)`
вместо `Expression(100)` на правом операнде.

## Антипаттерны, которые проект осознанно избегает

| Антипаттерн                                          | Что вместо                                |
| ---------------------------------------------------- | ----------------------------------------- |
| `eval()` / `DataTable.Compute()`                     | Свой Pratt-парсер                         |
| `double` для основных вычислений                     | `decimal`, double только в трансценд. ф-ях |
| Snapshot всей строки state в стеке undo              | Command Pattern с локальным захватом полей |
| `justEvaluated` + `nextClearsDisplay` булевы флаги   | enum `CalcPhase`                          |
| `txtDisplay.Text = ...` из обработчиков клика        | Bindings + ViewModel                      |
| Inline `Background="..."` в XAML                     | Только Styles + Resources                 |
| `Application.DoEvents()`, `Thread.Sleep` в UI потоке | Storyboard animations                     |
| `Focusable=True` на кнопках (ломает keyboard)        | `Focusable=False`, KeyBindings на Window  |

## Лицензия

MIT, или какую решишь — это твой проект.
