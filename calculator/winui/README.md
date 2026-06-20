# Calc Pro — WinUI 3 port

C# / WinUI 3 port of `calc-pro.html` (the Electron version).
Same calculator: Standard, Scientific, Fraction modes. Native Mica/Acrylic
backdrop instead of CSS-faked liquid glass.

## What's where

```
winui/
├── CalcPro.csproj              -- net8.0-windows10.0.19041, WinUI 3, unpackaged
├── app.manifest                -- per-monitor DPI awareness
├── App.xaml / App.xaml.cs      -- app entry, shared brushes + button styles
├── MainWindow.xaml / .cs       -- main UI, keypad rendering, key + window plumbing
└── Calculator/
    ├── Engine.cs               -- tokenise → RPN → eval (port of the JS parser)
    ├── Format.cs               -- formatNumber + decimal↔fraction
    ├── State.cs                -- CalcState model + FracTerm
    ├── HistoryStore.cs         -- JSON-on-disk replacement for localStorage
    ├── Input.cs                -- pressKey() for Standard/Scientific
    └── Fractions.cs            -- pressKeyFrac() + simplify + plain-text render
```

## Prerequisites

1. **Windows 10 build 17763+** (or any Windows 11).
2. **.NET 8 SDK**
   `winget install Microsoft.DotNet.SDK.8`
3. **Windows App SDK runtime** (only required if you build *framework-dependent*).
   `winget install Microsoft.WindowsAppRuntime.1.6`
4. (Optional) **Visual Studio 2022** with workloads:
   - `.NET desktop development`
   - `Windows application development`

VS handles everything automatically. CLI works too — see below.

## Build & run from CLI

```powershell
cd C:\Drive\Alexey\Calculator\winui

# Restore + build (debug)
dotnet build -c Debug -r win-x64

# Run
dotnet run -c Debug -r win-x64
```

## Build a portable .exe (matches your Electron `npm run build`)

Self-contained release, no runtime install needed on target machines:

```powershell
dotnet publish CalcPro.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:WindowsAppSDKSelfContained=true `
    /p:PublishSingleFile=false `
    -o publish
```

Output sits in `publish\` — the .exe is ~5 MB but it ships with native DLLs
(~80 MB total). Compared to your Electron portable (~150 MB) this is roughly
half the size and starts in well under a second.

## What's not (yet) ported

The C# code is feature-complete relative to `calc-pro.html`. UI niceties that
weren't carried over:

- **Ripple animation on button press** — easy to add via `ContentPresenter`
  + `Storyboard`, but it's pure cosmetics. Skipped for the first pass.
- **History panel** — replaced with a `MenuFlyout` on the history button.
  A proper slide-in panel needs a second view and a `Grid.RowDefinition`
  swap; deliberately left out.
- **Bouncy result animation** — placeholder hook `OnEvaluated()` is wired
  but empty. Plug in any `Storyboard` to taste.

Everything math-related is identical to the JS:
expression parsing, 3 modes, Rad/Deg/Grad, `2nd` flip, memory ops, history
persistence, Apple-style `%`, fraction conversion via continued fractions.

## Icon

Drop a 256×256 `app.ico` into `winui\Assets\app.ico` and it picks up
automatically (the .csproj references it under `<ApplicationIcon>`).

## Troubleshooting

**`Could not load file or assembly 'Microsoft.WindowsAppRuntime.Bootstrap.Net'`**
You're missing the Windows App SDK runtime. Either install it
(`winget install Microsoft.WindowsAppRuntime.1.6`) or build with
`--self-contained true /p:WindowsAppSDKSelfContained=true` so it ships with
the exe.

**`This project doesn't know how to run with profile 'CalcPro'`**
Use `dotnet run -r win-x64` — the RID is required because WinUI 3 doesn't
support `AnyCPU`.

**`XAMLDIAG2008: Cannot resolve 'XamlControlsResources'`**
The WinUI NuGet didn't restore. Run `dotnet restore` once, then try again.
