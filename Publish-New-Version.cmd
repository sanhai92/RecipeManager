@echo off
setlocal
cd /d "%~dp0"

for /f "usebackq tokens=*" %%v in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$xml=[xml](Get-Content '.\RecipeManager.csproj'); $xml.Project.PropertyGroup.Version"`) do set "VERSION=%%v"

if "%VERSION%"=="" (
    echo Could not read the app version from RecipeManager.csproj.
    pause
    exit /b 1
)

echo This publishes Recipe Manager version %VERSION%.
echo For every new update, increase the Version in RecipeManager.csproj first.
echo.

call ".\Publish-%VERSION%.cmd"
