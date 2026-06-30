module win32_example_gui_app;

import win32;

const uint GL_COLOR_BUFFER_BIT = 0x00004000;

bool running = true;
Hdc deviceContext;
HGlRc glContext;

fn LResult windowProc(HWnd hWnd, uint message, WParam wParam, LParam lParam) {
    if (message == win32::WM_CLOSE) {
        win32::destroyWindow(hWnd);
        return 0;
    }

    if (message == win32::WM_DESTROY) {
        running = false;
        win32::postQuitMessage(0);
        return 0;
    }

    return win32::defWindowProcW(hWnd, message, wParam, lParam);
}

fn bool setupOpenGl(HWnd window) {
    deviceContext = win32::getDC(window);
    if (deviceContext == null) return false;

    PixelFormatDescriptor format;
    format.nSize = (ushort)@sizeof(format);
    format.nVersion = 1;
    format.dwFlags = win32::PFD_DRAW_TO_WINDOW | win32::PFD_SUPPORT_OPENGL | win32::PFD_DOUBLEBUFFER;
    format.iPixelType = win32::PFD_TYPE_RGBA;
    format.cColorBits = (char)32;
    format.cRedBits = 0;
    format.cRedShift = 0;
    format.cGreenBits = 0;
    format.cGreenShift = 0;
    format.cBlueBits = 0;
    format.cBlueShift = 0;
    format.cAlphaBits = (char)8;
    format.cAlphaShift = 0;
    format.cAccumBits = 0;
    format.cAccumRedBits = 0;
    format.cAccumGreenBits = 0;
    format.cAccumBlueBits = 0;
    format.cAccumAlphaBits = 0;
    format.cDepthBits = (char)24;
    format.cStencilBits = (char)8;
    format.cAuxBuffers = 0;
    format.iLayerType = (char)win32::PFD_MAIN_PLANE;
    format.bReserved = 0;
    format.dwLayerMask = 0;
    format.dwVisibleMask = 0;
    format.dwDamageMask = 0;

    int pixelFormat = win32::choosePixelFormat(deviceContext, &format);
    if (pixelFormat == 0) return false;
    if (win32::setPixelFormat(deviceContext, pixelFormat, &format) == 0) return false;

    glContext = win32::wglCreateContext(deviceContext);
    if (glContext == null) return false;
    if (win32::wglMakeCurrent(deviceContext, glContext) == 0) return false;

    return true;
}

fn void render() {
    win32::glViewport(0, 0, 960, 540);
    win32::glClearColor(0.08f, 0.12f, 0.18f, 1.0f);
    win32::glClear(GL_COLOR_BUFFER_BIT);
    win32::swapBuffers(deviceContext);
}

fn void shutdownOpenGl(HWnd window) {
    if (glContext != null) {
        win32::wglMakeCurrent(null, null);
        win32::wglDeleteContext(glContext);
        glContext = null;
    }

    if (deviceContext != null) {
        win32::releaseDC(window, deviceContext);
        deviceContext = null;
    }
}

fn void main() {
    WString className = @wstring("C3GdiOpenGlExample");
    WString title = @wstring("C3 generated Win32 + OpenGL");
    HInstance instance = (HInstance)win32::getModuleHandleW(null);

    WndClassExW windowClass;
    windowClass.cbSize = (uint)@sizeof(windowClass);
    windowClass.style = win32::CS_HREDRAW | win32::CS_VREDRAW | win32::CS_OWNDC;
    windowClass.lpfnWndProc = &windowProc;
    windowClass.cbClsExtra = 0;
    windowClass.cbWndExtra = 0;
    windowClass.hInstance = instance;
    windowClass.hIcon = null;
    windowClass.hCursor = null;
    windowClass.hbrBackground = null;
    windowClass.lpszMenuName = null;
    windowClass.lpszClassName = &className[0];
    windowClass.hIconSm = null;

    win32::registerClassExW(&windowClass);

    HWnd window = win32::createWindowExW(
        0,
        &className[0],
        &title[0],
        win32::WS_OVERLAPPEDWINDOW | win32::WS_VISIBLE,
        win32::CW_USEDEFAULT,
        win32::CW_USEDEFAULT,
        960,
        540,
        null,
        null,
        instance,
        null
    );

    if (window == null) return;
    if (!setupOpenGl(window)) return;
    defer shutdownOpenGl(window);

    Msg message;
    while (running) {
        while (win32::peekMessageW(&message, null, 0, 0, win32::PM_REMOVE) != 0) {
            if (message.message == win32::WM_QUIT) {
                running = false;
                break;
            }

            win32::translateMessage(&message);
            win32::dispatchMessageW(&message);
        }

        if (running) render();
    }
}
