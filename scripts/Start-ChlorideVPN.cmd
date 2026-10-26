@echo off
setlocal
set "CHLORIDE_CLIENT_EXE=%~dp0v2rayN.exe"
powershell.exe -NoProfile -WindowStyle Hidden -Command "try { Start-Process -FilePath $env:CHLORIDE_CLIENT_EXE -ArgumentList '--show' -WorkingDirectory ([IO.Path]::GetDirectoryName($env:CHLORIDE_CLIENT_EXE)) -Verb RunAs -WindowStyle Normal -ErrorAction Stop } catch { exit 1 }"
exit /b %errorlevel%
