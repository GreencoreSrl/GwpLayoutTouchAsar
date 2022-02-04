echo off
del c:\server\casse\*.dat
del c:\server\casse\lan\*.dat
del c:\server\casse\lan\old\*.dat
del c:\server\casse\old\*.dat

call StopService.bat
echo copy GwpLayoutTouchAsar.exe C:\ServiziGWP\GwpLayoutTouchAsar\
copy GwpLayoutTouchAsar.exe C:\ServiziGWP\GwpLayoutTouchAsar\
pause
call StartService.bat