echo off
:inizio
cls
ECHO.
ECHO 1 - DEBUG
ECHO 2 - RELEASE
ECHO.

set /P PROGETTO=SCEGLI SCEGLI VERSIONE (DEBUG o RELEASE) : 

if %PROGETTO% EQU 1 goto debug
if %PROGETTO% EQU 2 goto release

goto inizio

:debug
set PERCORSO=..\bin\Debug\
goto comando

:release
set PERCORSO=..\bin\Release\
goto comando


:comando
del c:\server\casse\*.dat
del c:\server\casse\lan\*.dat
del c:\server\casse\lan\old\*.dat
del c:\server\casse\old\*.dat

call StopService.bat
echo copy %PERCORSO%GwpLayoutTouchAsar.exe C:\ServiziGWP\GwpLayoutTouchAsar\
copy %PERCORSO%GwpLayoutTouchAsar.exe C:\ServiziGWP\GwpLayoutTouchAsar\
pause
call StartService.bat

goto inizio