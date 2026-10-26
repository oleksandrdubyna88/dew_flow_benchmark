# Keeps Windows awake (system and display) while this process lives; no power settings are changed.
$sig = '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint f);'
$k = Add-Type -MemberDefinition $sig -Name KeepAwake -Namespace CoaiExp -PassThru
$prev = $k::SetThreadExecutionState([uint32]"0x80000003")
"keep-awake on pid=$PID prev=$prev"
try { Start-Sleep -Seconds 14400 } finally { $k::SetThreadExecutionState([uint32]"0x80000000") | Out-Null; "keep-awake off" }
