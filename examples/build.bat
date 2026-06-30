@echo off
echo Building win32_example_gui_app...
c3c compile -o win32_example_gui_app ..\out\win32.c3i win32_example_gui_app.c3
if %ERRORLEVEL% EQU 0 (
    echo Running win32_example_gui_app.exe...
    win32_example_gui_app.exe
) else (
    echo Build failed!
)
