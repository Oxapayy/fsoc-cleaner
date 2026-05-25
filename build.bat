@echo off
setlocal

set "PROJECT_DIR=%~dp0"
set "SOLUTION_DIR=%PROJECT_DIR%.."
set "OUTPUT_DIR=%SOLUTION_DIR%\Build"

echo.
echo ====================================
echo   Fsociety Cleaner - Build Script
echo ====================================
echo.

if exist "%OUTPUT_DIR%" (
    echo [1/3] Cleaning old build...
    rmdir /s /q "%OUTPUT_DIR%"
)

echo [2/3] Publishing release...
dotnet publish "%PROJECT_DIR%FsocietyCleaner.csproj" ^
    -c Release ^
    -p:PublishSingleFile=true ^
    --no-self-contained ^
    -o "%OUTPUT_DIR%"

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    pause
    exit /b 1
)

echo [3/3] Copying icon...
copy /y "%PROJECT_DIR%app.ico" "%OUTPUT_DIR%\app.ico" >nul

del /q "%OUTPUT_DIR%\*.pdb" 2>nul

echo.
echo ====================================
echo   BUILD SUCCESS
echo ====================================
echo   Output: %OUTPUT_DIR%\FsocietyCleaner.exe
echo.

for %%I in ("%OUTPUT_DIR%\FsocietyCleaner.exe") do echo   Size:   %%~zI bytes
echo.

pause
