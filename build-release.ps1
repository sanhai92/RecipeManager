param(
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.3',

    [Parameter(Mandatory = $false)]
    [string]$Repository = 'sanhai92/RecipeManager'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'
$installerDir = Join-Path $root 'artifacts\installer'

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $installerDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

$publishArgs = @(
    'publish', (Join-Path $root 'RecipeManager.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-o', $publishDir,
    "-p:Version=$Version",
    "-p:UpdateRepository=$Repository"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'The application publish step failed.' }

$compilerCandidates = @(
    (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

$compiler = $compilerCandidates | Select-Object -First 1
if (-not $compiler) {
    Write-Host "Published app created at: $publishDir"
    Write-Host 'Install Inno Setup 6, then run this script again to create the installer.'
    exit 0
}

& $compiler "/DMyAppVersion=$Version" (Join-Path $root 'installer\RecipeManager.iss')
if ($LASTEXITCODE -ne 0) { throw 'The installer compilation step failed.' }

Write-Host "Installer created at: $(Join-Path $installerDir "RecipeManager-Setup-$Version.exe")"
