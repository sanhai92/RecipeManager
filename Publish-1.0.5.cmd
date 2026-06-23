@echo off
setlocal
cd /d "%~dp0"

set "VERSION=1.0.5"

echo Preparing Recipe Manager %VERSION%...
git add -A || goto :failed
git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Release Recipe Manager %VERSION%" || goto :failed
)

echo Bringing in the latest GitHub changes...
git fetch origin || goto :failed
git merge -X ours origin/main --no-edit || goto :failed

echo Running the release build...
dotnet build RecipeManager.csproj -c Release --no-restore || goto :failed

git rev-parse --verify --quiet refs/tags/v%VERSION% >NUL
if not errorlevel 1 (
    echo Tag v%VERSION% already exists locally. Nothing was pushed.
    goto :failed
)

git tag -a v%VERSION% -m "Recipe Manager %VERSION%" || goto :failed

echo Publishing source and release tag...
git push origin main || goto :failed
git push origin v%VERSION% || goto :failed

echo.
echo Recipe Manager %VERSION% was pushed successfully.
echo GitHub is now building the Windows installer and update.
pause
exit /b 0

:failed
echo.
echo Publishing stopped. Read the message above; no remaining steps were attempted.
pause
exit /b 1
