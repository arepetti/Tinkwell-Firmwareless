# Paths and filenames
$partitionsCsv = "./partitions.csv"
$appBin = "./build/tw-therm-dr.bin"
$tempDir = "./temp"
$baseFlash = "$tempDir/qemu_flash_base.bin"
$patchSource = "$tempDir/qemu_flash_after_run.bin"
$patchedFlash = "$tempDir/qemu_flash.bin"

Write-Host "`e[34mPreparing environment`e[0m"
if (-Not $env:IDF_PATH) {
    if (Test-Path "../esp-idf/export.ps1") {
        . "../esp-idf/export.ps1"
    } elseif (Test-Path "../../esp-idf/export.ps1") {
        . "../../esp-idf/export.ps1"
    }
}

# Ensure we're in the project root
if (-Not ((Test-Path "./CMakeLists.txt") -and (Test-Path "./sdkconfig"))) {
    Set-Location ./src
}

# Make sure temp directory exists
if (-Not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

# Load partition info
function ConvertTo-Number {
    param([string]$s)
    $s = $s.Trim()
    if ($s -match '^\s*0x([0-9A-Fa-f]+)\s*$') {
        return [Convert]::ToInt32($Matches[1], 16)
    }
    elseif ($s -match '^\d+$') {
        return [int]$s
    }
    else {
        throw "Invalid numeric value '$s'"
    }
}

function Get-PartitionOffset($name) {
    $pattern = '^' + [regex]::Escape($name) + '\s*,'
    $line = Get-Content $partitionsCsv | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if (-not $line) { throw "Partition '$name' not found in partitions.csv" }
    $cols = $line -split ','
    return ConvertTo-Number $cols[3]
}

function Get-PartitionLength($name) {
    $pattern = '^' + [regex]::Escape($name) + '\s*,'
    $line = Get-Content $partitionsCsv | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if (-not $line) { throw "Partition '$name' not found in partitions.csv" }
    $cols = $line -split ','
    return ConvertTo-Number $cols[4]
}

Write-Host "`e[34mReading partition table`e[0m"
$factoryOffset = Get-PartitionOffset "factory"
$configOffset = Get-PartitionOffset "config"
$configLength = Get-PartitionLength "config"
$configEnd = $configOffset + $configLength

Write-Host "Factory offset: 0x$("{0:X}" -f $factoryOffset)"
Write-Host "Config offset at: 0x$("{0:X}" -f $configOffset)"
Write-Host "Config ends at: 0x$("{0:X}" -f $configEnd)"

# Build the project
Write-Host "`e[34mBuilding project`e[0m"
idf.py build

# Generate base image manually using esptool, we cannot use "idf.py qemu"
# directly because we want to patch the image with the latest saved data.
Write-Host "`e[34mCreating flash binary`e[0m"
$mergeArgs = @(
    "--chip", "esp32",
    "merge_bin",
    "--output", $baseFlash,
    "--fill-flash-size", "4MB",
    "--flash_mode", "dio",
    "--flash_freq", "40m",
    "--flash_size", "4MB",
    "0x1000", "./build/bootloader/bootloader.bin",
    "0x8000", "./build/partition_table/partition-table.bin",
    ("0x{0:x}" -f $factoryOffset), $appBin
)


& esptool.py @mergeArgs

# Patch base image with config from previous run
if (Test-Path $patchSource) {
    Write-Host "`e[34mPatching configuration`e[0m"
    $baseBytes = [System.IO.File]::ReadAllBytes((Resolve-Path $baseFlash).Path)
    $patchBytes = [System.IO.File]::ReadAllBytes((Resolve-Path $patchSource).Path)
    [Array]::Copy($patchBytes, $configOffset, $baseBytes, $configOffset, $configLength)
    [System.IO.File]::WriteAllBytes((Resolve-Path $patchedFlash).Path, $baseBytes)
} else {
    Write-Host "`e[34mCopying merged binary`e[0m"
    Copy-Item $baseFlash $patchedFlash -Force
}

# Run QEMU with patched image
Write-Host "`e[34mLaunching QEMU`e[0m"
idf.py qemu --flash-file $patchedFlash --qemu-extra-args "-nic user,model=open_eth,id=lo0,hostfwd=tcp::32080-:32080"

# Save new image after run (if modified during QEMU execution)
Write-Host "`e[34mSaving modified image`e[0m"
Copy-Item $patchedFlash $patchSource -Force