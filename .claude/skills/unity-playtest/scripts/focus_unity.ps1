Add-Type @'
using System; using System.Runtime.InteropServices;
public class WFocus { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); }
'@
$p = Get-Process Unity | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
[WFocus]::SetForegroundWindow($p.MainWindowHandle)
