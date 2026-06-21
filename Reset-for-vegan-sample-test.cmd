@echo off
setlocal

tasklist /FI "IMAGENAME eq RecipeManager.exe" 2>NUL | find /I "RecipeManager.exe" >NUL
if not errorlevel 1 (
    echo Recipe Manager is still running. Close it and run this file again.
    pause
    exit /b 1
)

set "APPDIR=%LOCALAPPDATA%\RecipeManager"
set "DB=%APPDIR%\recipes.db"
set "BACKUPDIR=%APPDIR%\Backups"

if not exist "%DB%" (
    echo No current recipe database was found.
    echo Open Recipe Manager to create the vegan sample database.
    pause
    exit /b 0
)

if not exist "%BACKUPDIR%" mkdir "%BACKUPDIR%"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%I"
set "BACKUP=%BACKUPDIR%\recipes-before-vegan-sample-test-%STAMP%.db"

copy /Y "%DB%" "%BACKUP%" >NUL
if errorlevel 1 (
    echo The safety backup could not be created. Nothing was deleted.
    pause
    exit /b 1
)

del /Q "%DB%"
if exist "%DB%-wal" del /Q "%DB%-wal"
if exist "%DB%-shm" del /Q "%DB%-shm"
if exist "%DB%-journal" del /Q "%DB%-journal"

echo Database reset complete.
echo Safety backup: %BACKUP%
echo.
set "TESTAPP=%~dp0bin\Release\net8.0-windows\RecipeManager.exe"
if exist "%TESTAPP%" (
    echo Opening the latest local test version with vegan sample recipes...
    start "" "%TESTAPP%"
) else (
    echo The latest local test app was not found. Build it before opening Recipe Manager.
    pause
)
