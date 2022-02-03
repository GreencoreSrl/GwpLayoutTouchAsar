@ECHO OFF
@ECHO INSTALL WINDOWS SERVICE
path=%path%;C:\Windows\Microsoft.NET\Framework64\v4.0.30319
rem cd\
rem cd C:\Windows\Microsoft.NET\Framework\v4.0.30319
installutil.exe "C:\ServiziGWP\GwpLayoutTouchAsar\GwpLayoutTouchAsar.exe"
sc.exe failure GwpLayoutTouchAsar reset= 86400 actions= restart/5000/restart/5000/restart/5000
PAUSE

