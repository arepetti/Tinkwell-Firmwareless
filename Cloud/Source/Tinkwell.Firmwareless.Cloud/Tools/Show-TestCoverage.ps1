# Define paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$solutionDir = Join-Path $scriptDir ".."
$unitTestProject = Join-Path $solutionDir "Tests/Tinkwell.Firmwareless.PublicRepository.UnitTests/Tinkwell.Firmwareless.PublicRepository.UnitTests.csproj"
$integrationTestProject = Join-Path $solutionDir "Tests/Tinkwell.Firmwareless.PublicRepository.IntegrationTests/Tinkwell.Firmwareless.PublicRepository.IntegrationTests.csproj"
$runSettingsFile = Join-Path $solutionDir "coverlet.runsettings"
$coverageReportDir = Join-Path $solutionDir "coverage_report"

# Ensure TestResults directory is clean
Remove-Item (Join-Path $solutionDir "Tests/*/TestResults") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Running Unit Tests and collecting coverage..."
dotnet test $unitTestProject `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="$(Join-Path $solutionDir 'Tests/Tinkwell.Firmwareless.PublicRepository.UnitTests/TestResults/')" `
    /p:RunSettingsFilePath="$runSettingsFile"

Write-Host "Running Integration Tests and collecting coverage..."
dotnet test $integrationTestProject `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="$(Join-Path $solutionDir 'Tests/Tinkwell.Firmwareless.PublicRepository.IntegrationTests/TestResults/')" `
    /p:RunSettingsFilePath="$runSettingsFile"

Write-Host "Generating HTML coverage report..."
# Find all coverage.cobertura.xml files
$coverageFiles = Get-ChildItem -Path (Join-Path $solutionDir "Tests") -Recurse -Include "coverage.cobertura.xml" | Select-Object -ExpandProperty FullName
$reportsArg = "-reports:" + ($coverageFiles -join ";")

# Ensure ReportGenerator is installed
if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    Write-Host "ReportGenerator is not installed. Installing now..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

reportgenerator $reportsArg `
    "-targetdir:$coverageReportDir" `
    "-reporttypes:Html"

Write-Host "Opening report in browser..."
Start-Process (Join-Path $coverageReportDir "index.html")

Write-Host "Done."
