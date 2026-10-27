# Exit 0 when the experiment's keep-awake runs, 1 when it does not. "\keep-awake.ps1" does not match stop-/check- scripts.
$p = Get-CimInstance Win32_Process -Filter "Name = 'pwsh.exe'" | Where-Object { $_.CommandLine -like '*\keep-awake.ps1*' }
if ($p) { "running: $($p.ProcessId -join ',')"; exit 0 } else { "none"; exit 1 }
