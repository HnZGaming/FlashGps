@echo on
REM --- List your target paths below, separated by spaces ---
REM Example: D:\Games\SE\Mods D:\Backup\Mods
set "targets=D:\steamapps\workshop\content\244850 C:\torch-server\Instance\content\244850 C:\torch-server-pvp\Instance\content\244850"
@echo off
setlocal enabledelayedexpansion

REM Get the directory of this script

set "script_dir=%~dp0"
REM Remove trailing backslash if present
if "%script_dir:~-1%"=="\" set "script_dir=%script_dir:~0,-1%"
echo [DEBUG] Script directory: %script_dir%

REM Use absolute path for modinfo.sbmi

set "modinfo=%script_dir%\Content\modinfo.sbmi"
set "content_dir=%script_dir%\Content"
echo [DEBUG] modinfo.sbmi path: %modinfo%
echo [DEBUG] Content folder path: %content_dir%
set "workshopid="
for /f "tokens=2 delims=>" %%A in ('findstr /C:"<Id>" "%modinfo%"') do (
	for /f "delims=<" %%B in ("%%A") do set "workshopid=%%B"
)
echo [DEBUG] Workshop ID: !workshopid!


if not defined workshopid (
	echo [ERROR] Workshop ID not found in %modinfo%.
	exit /b 1
)

REM Create symlinks for each target

for %%T in (%targets%) do (
	echo [DEBUG] Target path: %%T
	if not exist "%%T" (
		echo [ERROR] Target directory does not exist: %%T
		continue
	)
	if exist "%%T\!workshopid!" (
		echo [INFO] Removing existing folder or symlink: %%T\!workshopid!
		rmdir /S /Q "%%T\!workshopid!"
	)
	echo [INFO] Creating symlink: %%T\!workshopid! -> !content_dir!
	mklink /D "%%T\!workshopid!" "!content_dir!"
)

endlocal
exit /b
