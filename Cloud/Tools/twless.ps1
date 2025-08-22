param (
    [Parameter(Mandatory=$true)]
    [string]$Domain,

    [Parameter(Mandatory=$true)]
    [string]$Verb,

    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$OtherArgs
)

$baseName = "twless"
$targetScript = "$baseName-$Domain-$Verb.py"
$scriptPath = Join-Path $PSScriptRoot $targetScript

if (-not (Test-Path $scriptPath)) {
    Write-Error "Error: script '$targetScript' not found in current directory"
    exit 1
}

$python = (Get-Command python).Source

$envDir = ".penv"

if (-Not (Test-Path $envDir)) {
    Write-Host "Creating virtual environment in $envDir..."
    python -m venv $envDir

    Write-Host "Installing dependencies..."
    & "$envDir\Scripts\Activate.ps1"
    python -m pip install --upgrade pip
    pip install -r requirements.txt
    Write-Host "Virtual environment is now active"
} else {
    & "$envDir\Scripts\Activate.ps1"
}

& $python $scriptPath @OtherArgs

