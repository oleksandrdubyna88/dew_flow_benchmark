# Stops every pwsh process that runs keep-awake.ps1 (the experiment's temporary keep-awake), and nothing else.
Get-CimInstance Win32_Process -Filter "Name = 'pwsh.exe'" |
  Where-Object { $_.CommandLine -like '*\keep-awake.ps1*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force; "stopped keep-awake pid $($_.ProcessId)" }
"done"
