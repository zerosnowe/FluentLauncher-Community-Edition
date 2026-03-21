$localizerProject = ".\FluentLauncher.Infra.Localization\FluentLauncher.Infra.Localizer"

# Ensure dotnet commands launched by this script work in restricted environments.
if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot ".dotnet_cli_home"
}
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Check if localizer exists
if (-not (Test-Path $localizerProject)) {
    Write-Host "Localization tool not found! Check the localization project submodule."
    exit 1
}

# Check modification dates
$csvFolder = ".\FluentLauncher.Infra.Localization\Views"
$reswFolder = ".\Natsurainko.FluentLauncher\Assets\Strings"

# Function to get the most recent modification date in a directory
function Get-MostRecentModificationDate {
    param (
        [string]$path
    )
    Get-ChildItem -Path $path -Recurse | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 -ExpandProperty LastWriteTime
}

# Get the most recent modification dates
$latestCsvDate = Get-MostRecentModificationDate -path $csvFolder
$latestReswDate = Get-MostRecentModificationDate -path $reswFolder

# Compare dates and exit if no compilation is needed
if ($latestReswDate -ge $latestCsvDate) {
    Write-Host "Skipped generation of resw files. Translations are up-to-date."
    exit 0
}

# Generate .resw files if trnaslations are updated
dotnet run --no-restore --project $localizerProject -- --src $csvFolder --out $reswFolder --languages en-US zh-Hans zh-Hant ru-RU uk-UA --default-language en-US
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
exit 0
