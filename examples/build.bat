@echo off
setlocal EnableDelayedExpansion
echo Building win32_example_gui_app...
set C3I_FILES=
for %%F in (..\out\win32\*.c3i) do set C3I_FILES=!C3I_FILES! "%%F"
c3c compile -o win32_example_gui_app %C3I_FILES% win32_example_gui_app.c3
if %ERRORLEVEL% EQU 0 (
    echo Running win32_example_gui_app.exe...
    win32_example_gui_app.exe
) else (
    echo Build failed!
)
