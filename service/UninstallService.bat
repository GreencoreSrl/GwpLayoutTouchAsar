@ECHO OFF
@ECHO UNINSTALL WINDOWS SERVICE
path=%path%;C:\Windows\Microsoft.NET\Framework64\v4.0.30319
rem cd\
rem cd C:\Windows\Microsoft.NET\Framework\v4.0.30319
installutil.exe -u " C:\ServiziGWP\GwpLayoutTouchAsar\GwpLayoutTouchAsar.exe"
PAUSE

