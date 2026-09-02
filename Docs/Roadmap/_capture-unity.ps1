Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32j {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Write-Output '---unity---'
Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object {
  "pid=$($_.Id) title='$($_.MainWindowTitle)' handle=$($_.MainWindowHandle)"
}
$unity = Get-Process Unity -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero -and $_.MainWindowTitle -match 'solar-majesty|Unity' } |
  Sort-Object { if ($_.MainWindowTitle -match 'solar-majesty') { 0 } else { 1 } } |
  Select-Object -First 1
if (-not $unity) {
  $unity = Get-Process | Where-Object { $_.ProcessName -match '^Unity' -and $_.MainWindowHandle -ne [IntPtr]::Zero -and $_.MainWindowTitle -ne '' } |
    Select-Object -First 1
}
if (-not $unity) { Write-Output 'NO_UNITY'; exit 1 }
$h = $unity.MainWindowHandle
$r0 = New-Object Win32j+RECT
[void][Win32j]::GetWindowRect($h, [ref]$r0)
Write-Output "PICK pid=$($unity.Id) title='$($unity.MainWindowTitle)' rect=$($r0.Left),$($r0.Top) $($r0.Right-$r0.Left)x$($r0.Bottom-$r0.Top) iconic=$([Win32j]::IsIconic($h)) zoomed=$([Win32j]::IsZoomed($h))"
if ([Win32j]::IsIconic($h)) { [void][Win32j]::ShowWindow($h, 9) }
elseif (-not [Win32j]::IsZoomed($h)) { [void][Win32j]::ShowWindow($h, 3) }
[void][Win32j]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 800
$r = New-Object Win32j+RECT
[void][Win32j]::GetWindowRect($h, [ref]$r)
$w = [Math]::Max(1, $r.Right - $r.Left)
$ht = [Math]::Max(1, $r.Bottom - $r.Top)
Write-Output "after rect=$($r.Left),$($r.Top) ${w}x${ht} zoomed=$([Win32j]::IsZoomed($h))"
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $ht))
$out = 'C:\Users\Aaron\source\repos\ajesbenshade\solar-majesty\Docs\Roadmap\SM_MarsCampaign_PlayModeCampusStill10.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved still10 ${w}x${ht}"
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$full = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
$gf = [System.Drawing.Graphics]::FromImage($full)
$gf.CopyFromScreen($vs.X, $vs.Y, 0, 0, $vs.Size)
$fullPath = 'C:\Users\Aaron\source\repos\ajesbenshade\solar-majesty\Docs\Roadmap\_desktop_now.png'
$full.Save($fullPath, [System.Drawing.Imaging.ImageFormat]::Png)
$gf.Dispose(); $full.Dispose()
Write-Output "saved desktop $($vs.Width)x$($vs.Height)"
