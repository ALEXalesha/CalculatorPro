$ErrorActionPreference = 'SilentlyContinue'

$signature = @"
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")] public static extern bool IsIconic(System.IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool IsWindow(System.IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr hWnd);
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
"@
Add-Type -MemberDefinition $signature -Name 'U32' -Namespace 'WinAPI'

# kill old
Get-Process electron -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Start-Sleep -Seconds 1

# launch
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
Set-Location "C:\Drive\Alexey\CalculatorNEW\calculator-app"
Start-Process -FilePath "node_modules\.bin\electron.cmd" -ArgumentList "."

# wait for window via Get-Process
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 30; $i++) {
  Start-Sleep -Milliseconds 500
  $p = Get-Process electron -EA SilentlyContinue | Where-Object { $_.MainWindowTitle -eq 'Calculator iOS 26' } | Select-Object -First 1
  if ($p -and $p.MainWindowHandle -ne 0) {
    $hwnd = $p.MainWindowHandle
    break
  }
}

if ($hwnd -eq [IntPtr]::Zero) {
  Write-Host "[FAIL] Window not found after 15s"
  Get-Process electron -EA SilentlyContinue | Stop-Process -Force
  exit 1
}

Write-Host "[OK] Window found HWND=$hwnd"
[WinAPI.U32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Seconds 1

$rect = New-Object WinAPI.U32+RECT
[WinAPI.U32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
Write-Host "Window rect: L=$($rect.Left) T=$($rect.Top) R=$($rect.Right) B=$($rect.Bottom)"

# coords (top:8 right:10, 26x26, gap 6)
$closeX = $rect.Right - 10 - 13
$closeY = $rect.Top + 8 + 13
$minX = $closeX - 26 - 6
$minY = $closeY
Write-Host "Min btn at ($minX, $minY); Close btn at ($closeX, $closeY)"

# === TEST 1: minimize ===
Write-Host ""
Write-Host "TEST 1: Real OS click on minimize"
[WinAPI.U32]::SetCursorPos($minX, $minY) | Out-Null
Start-Sleep -Milliseconds 300
[WinAPI.U32]::mouse_event(0x02, 0, 0, 0, 0)
Start-Sleep -Milliseconds 80
[WinAPI.U32]::mouse_event(0x04, 0, 0, 0, 0)
Start-Sleep -Seconds 2

$isMin = [WinAPI.U32]::IsIconic($hwnd)
Write-Host "  -> Minimized: $isMin"
$test1Pass = $isMin

# restore
[WinAPI.U32]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Seconds 1
[WinAPI.U32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500
[WinAPI.U32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$closeX = $rect.Right - 10 - 13
$closeY = $rect.Top + 8 + 13

# === TEST 2: close ===
Write-Host ""
Write-Host "TEST 2: Real OS click on close"
[WinAPI.U32]::SetCursorPos($closeX, $closeY) | Out-Null
Start-Sleep -Milliseconds 300
[WinAPI.U32]::mouse_event(0x02, 0, 0, 0, 0)
Start-Sleep -Milliseconds 80
[WinAPI.U32]::mouse_event(0x04, 0, 0, 0, 0)
Start-Sleep -Seconds 2

$stillExists = [WinAPI.U32]::IsWindow($hwnd)
Write-Host "  -> Window still exists: $stillExists"
$test2Pass = -not $stillExists

# cleanup
Get-Process electron -EA SilentlyContinue | Stop-Process -Force

Write-Host ""
Write-Host "=== RESULTS ==="
if ($test1Pass) { Write-Host "Minimize button: PASS" } else { Write-Host "Minimize button: FAIL" }
if ($test2Pass) { Write-Host "Close button:    PASS" } else { Write-Host "Close button:    FAIL" }
