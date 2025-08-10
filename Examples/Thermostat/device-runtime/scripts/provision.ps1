<#
.SYNOPSIS
  Send commands to a provisioning TCP service using ncat.

.EXAMPLE
  ./provisioning.ps1 -Host 192.168.1.2 -Port 32080 -CommandsFile .\commands.txt
#>

param(
  [Parameter(Mandatory=$true)]
  [string]$CommandsFile,

  [string]$TcpHost = "127.0.0.1",
  [int]$Port = 4998,

  [int]$TimeoutMs = 10000,

  # If ncat isn't in PATH, set the full path here (e.g., "C:\Program Files\Nmap\ncat.exe")
  [string]$NcatPath = "ncat",

  # Optional: extra args for ncat (e.g., "-v" or "--ssl")
  [string]$NcatArgs = ""
)

if (-not (Test-Path -LiteralPath $CommandsFile)) {
  throw "Commands file not found: $CommandsFile"
}

# Read commands, skipping empty lines and comments
$Commands = Get-Content -LiteralPath $CommandsFile |
  Where-Object { $_ -ne $null -and $_.Trim() -ne "" -and -not $_.Trim().StartsWith('#') }

if (-not $Commands -or $Commands.Count -eq 0) {
  throw "No commands to send after filtering empty/comment lines in $CommandsFile"
}

# Prepare ncat process
$proc = New-Object System.Diagnostics.Process
$si = $proc.StartInfo
$si.FileName = $NcatPath
$si.Arguments = ($NcatArgs.Trim()) ? "$NcatArgs $Host $Port" : "$Host $Port"
$si.RedirectStandardInput  = $true
$si.RedirectStandardOutput = $true
$si.RedirectStandardError  = $true
$si.UseShellExecute = $false
$si.CreateNoWindow  = $true

# Queues for async output capture
$stdoutQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
$stderrQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()

# Output handlers
$handlerOut = [System.Diagnostics.DataReceivedEventHandler]{
  param($s, $e)
  if ($e.Data -ne $null) { [void]$stdoutQueue.Enqueue($e.Data) }
}
$handlerErr = [System.Diagnostics.DataReceivedEventHandler]{
  param($s, $e)
  if ($e.Data -ne $null) { [void]$stderrQueue.Enqueue($e.Data) }
}

# Start ncat and begin async reads
if (-not $proc.Start()) { throw "Failed to start ncat" }
$proc.add_OutputDataReceived($handlerOut)
$proc.add_ErrorDataReceived($handlerErr)
$proc.BeginOutputReadLine()
$proc.BeginErrorReadLine()

$writer = $proc.StandardInput
$writer.NewLine = "`r`n"
$writer.AutoFlush = $true

function Use-Stderr {
  while ($true) {
    $line = $null
    if (-not $stderrQueue.TryDequeue([ref]$line)) { break }
    Write-Warning ("[ncat:stderr] " + $line)
  }
}

function Wait-ForResponse {
  param(
    [int]$Timeout,
    [string]$OkRegex,
    [string]$ErrRegex
  )
  $deadline = [DateTime]::UtcNow.AddMilliseconds($Timeout)
  $accum = New-Object System.Text.StringBuilder
  while ([DateTime]::UtcNow -lt $deadline) {
    # Clear stdout lines
    $line = $null
    $sawLine = $false
    while ($stdoutQueue.TryDequeue([ref]$line)) {
      $sawLine = $true
      [void]$accum.AppendLine($line)
      Write-Host $line
      if ($line -eq "OK")  { return @{ Status="OK"; Raw=$accum.ToString() } }
      if ($line.StartsWith("E")) { return @{ Status="ERROR"; Raw=$accum.ToString() } }
    }
    # Clear any stderr lines
    Use-Stderr

    if ($proc.HasExited) { return @{ Status="DISCONNECTED"; Raw=$accum.ToString() } }

    if (-not $sawLine) { Start-Sleep -Milliseconds 50 }
  }
  return @{ Status="TIMEOUT"; Raw=$accum.ToString() }
}

try {
  foreach ($cmd in $Commands) {
    Write-Host ">> $cmd"
    $writer.WriteLine($cmd)

    $res = Wait-ForResponse -Timeout $TimeoutMs -OkRegex $OkPattern -ErrRegex $ErrPattern
    switch ($res.Status) {
      "OK"          { continue }
      "ERROR"       { Write-Warning "Device returned ERROR for '$cmd'"; break }
      "TIMEOUT"     { Write-Warning "Timeout waiting for OK for '$cmd'"; break }
      "DISCONNECTED"{ Write-Information "ncat disconnected"; break }
      default       { Write-Warning "Unexpected status '$($res.Status)'"; break }
    }
  }
}
finally {
  try { Use-Stderr } catch {}
  try { if ($writer) { $writer.Close() } } catch {}
  try {
    if ($proc -and -not $proc.HasExited) { $proc.Kill() | Out-Null }
  } catch {}
  try {
    if ($proc) {
      $proc.remove_OutputDataReceived($handlerOut)
      $proc.remove_ErrorDataReceived($handlerErr)
      $proc.Dispose()
    }
  } catch {}
}