@echo off
setlocal EnableExtensions
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
set "PYEXE="

for /f "delims=" %%I in ('py -3 -c "import sys; print(sys.executable)" 2^>nul') do set "PYEXE=%%I"
if defined PYEXE goto :run

for /f "delims=" %%I in ('where python 2^>nul') do (
  call :try "%%I"
  if defined PYEXE goto :run
)
for /f "delims=" %%I in ('where python3 2^>nul') do (
  call :try "%%I"
  if defined PYEXE goto :run
)

echo Python 3 not found. Install Python, or use the "py" launcher.
pause
exit /b 1

:try
echo %~1 | find /i "\WindowsApps\" >nul && goto :eof
if not exist "%~1" goto :eof
"%~1" -c "import sys" >nul 2>&1
if errorlevel 1 goto :eof
set "PYEXE=%~1"
goto :eof

:run
if not exist "%~dp0scripts\launch_parse.py" (
  echo Missing scripts\launch_parse.py
  pause
  exit /b 1
)
"%PYEXE%" "%~dp0scripts\launch_parse.py"
if errorlevel 1 pause
exit /b %ERRORLEVEL%
