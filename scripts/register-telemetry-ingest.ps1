# Puts telemetry-ingest.ps1 on a daily schedule — the missing invocation the 2026-08-19 stability
# audit named: the spool's lifecycle has an owner (this repository's ingester) that nothing called.
#
#   Unregister-ScheduledTask dew_flow-telemetry-ingest -Confirm:$false   # to remove
#   Start-ScheduledTask dew_flow-telemetry-ingest                        # to run one now

$ErrorActionPreference = 'Stop'

$name = 'dew_flow-telemetry-ingest'
$pwsh = (Get-Command pwsh).Source
$script = Join-Path $PSScriptRoot 'telemetry-ingest.ps1'

$action = New-ScheduledTaskAction -Execute $pwsh `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""

# 03:30 daily — the machine's quietest hour; StartWhenAvailable catches up after sleep, so a laptop
# lid does not turn "daily" into "never".
$trigger = New-ScheduledTaskTrigger -Daily -At '03:30'

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries

$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Force | Out-Null

Write-Host "Registered '$name': daily at 03:30, ingest then prune (>14 days of *.ingested)."
