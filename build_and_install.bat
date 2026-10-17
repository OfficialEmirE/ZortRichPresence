@echo off
echo Building Zort Rich Presence Mod...

REM Build the project
dotnet build ZortRichPresence.csproj --configuration Release

REM Check if build was successful
if %ERRORLEVEL% NEQ 0 (
    echo Build failed! Check the errors above.
    pause
    exit /b 1
)

echo Build successful!

REM Define source and destination paths
set SOURCE_DLL=bin\Release\netstandard2.1\ZortRichPresence.dll
set DEST_FOLDER=D:\Steam\steamapps\common\Zort\BepInEx\plugins

REM Check if source file exists
if not exist "%SOURCE_DLL%" (
    echo Error: Compiled DLL not found at %SOURCE_DLL%
    echo Trying Debug build...
    set SOURCE_DLL=bin\Debug\netstandard2.1\ZortRichPresence.dll
    if not exist "%SOURCE_DLL%" (
        echo Error: DLL not found in Debug folder either!
        pause
        exit /b 1
    )
)

REM Check if destination folder exists
if not exist "%DEST_FOLDER%" (
    echo Creating destination folder...
    mkdir "%DEST_FOLDER%"
)

REM Copy the DLL to the game's plugins folder
echo Copying mod to game folder...
copy "%SOURCE_DLL%" "%DEST_FOLDER%"

REM Copy Discord RPC runtime dependencies required by the mod
set BUILD_FOLDER=bin\Release\netstandard2.1
if not exist "%BUILD_FOLDER%\DiscordRPC.dll" (
    echo Error: DiscordRPC.dll not found in %BUILD_FOLDER%
    pause
    exit /b 1
)
copy "%BUILD_FOLDER%\DiscordRPC.dll" "%DEST_FOLDER%"
copy "%BUILD_FOLDER%\Newtonsoft.Json.dll" "%DEST_FOLDER%"
copy "ZortLib.dll" "%DEST_FOLDER%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo SUCCESS! Mod has been installed!
    echo ========================================
    echo.
    echo The mod has been copied to:
    echo %DEST_FOLDER%
    echo.
    echo You can now start the game and use
    echo the Zort Rich Presence mod!
    echo.
) else (
    echo.
    echo ========================================
    echo ERROR! Failed to copy mod!
    echo ========================================
    echo.
    echo Please check if the game folder exists:
    echo %DEST_FOLDER%
    echo.
)

pause
