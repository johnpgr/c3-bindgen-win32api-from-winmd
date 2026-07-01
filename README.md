# c3-bindgen-win32api-from-winmn — C3 Win32 Bindings Generator

`c3-bindgen-win32api-from-winmn` is a .NET utility designed to automatically generate C3 language bindings (`.c3i` interface files) from Microsoft's Win32 Metadata (`Windows.Win32.winmd` ECMA-335 assemblies).

---

## Table of Contents
1. [Why this Generator? (C3 Interop & Casing Rules)](#why-this-generator-c3-interop--casing-rules)
2. [Prerequisites](#prerequisites)
3. [How to Run & Generate Bindings](#how-to-run--generate-bindings)
4. [Using the JSON Subset Configuration](#using-the-json-subset-configuration)
5. [Compiling & Using the Bindings in C3](#compiling--using-the-bindings-in-c3)
6. [Example GUI Application](#example-gui-application)

---

## Why this Generator? (C3 Interop & Casing Rules)

In C3, naming rules are enforced directly by the lexer and parser. Unlike Odin or Zig, which can import C headers and preserve their exact casing (e.g. `HWND` or `RegisterClassExW`), C3 requires:
* **Types** to start with an uppercase letter and contain at least one lowercase character (e.g., `HWND -> HWnd`, `HINSTANCE -> HInstance`, `WNDCLASSEXW -> WndClassExW`).
* **Functions, variables, and parameters** to start with a lowercase letter. This generator emits functions in C3-style `snake_case` while preserving final Win32 `A`/`W` suffixes (e.g., `RegisterClassExW -> register_class_exW`, `GetModuleHandleW -> get_module_handleW`).

To resolve this naming mismatch without losing the correct ABI alignment, `c3-bindgen-win32api-from-winmn` acts as a name projector. It maps Win32 structures to valid C3 identifiers while preserving the original linker symbols using C3's `@cname` attribute:

```c3
// Win32 original: RegisterClassExW
extern fn Atom register_class_exW(WndClassExW* lpwcx)
    @cname("RegisterClassExW");
```

It also maps Win32 parameter direction metadata (`[In]`, `[Out]`) to C3 pointer contracts:
```c3
// Win32 original: GetWindowRect
<*
 @param [out] lpRect
*>
extern fn Bool get_window_rect(HWnd hWnd, Rect* lpRect)
    @cname("GetWindowRect");
```

---

## Prerequisites

* [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
* [C3 Compiler (`c3c`)](https://c3-lang.org/) (for compiling C3 applications)

---

## How to Run & Generate Bindings

### 1. Run Self-Tests
Verify your environment and dependencies by running the built-in self-tests:
```powershell
dotnet run -- --self-test
```

### 2. Generate C3 Bindings
Generate bindings by specifying the path to the `.winmd` file, your JSON subset specification, and the output path:
```powershell
dotnet run -- --winmd data/Windows.Win32.winmd --subset data/win32.json --out out/win32 --db out/bindgen-runs.sqlite
```

#### CLI Parameters:
* `--winmd <path>`: (Required) Path to Microsoft's `Windows.Win32.winmd` file.
* `--subset <path>`: Path to a JSON subset definition (defaults to `data/window-subset.json`).
* `--out <path>`: Path to the generated C3 interface output directory (defaults to `out/win32`).
* `--db <path>`: Optional path to write a SQLite runs metadata database logging generation runs.
* `--dump-json <path>`: Optional path to write the raw, un-subsetted api metadata as JSON.
* `--self-test`: Runs the project's internal unit tests.
* `-h` / `--help`: Shows command usage help.

---

## Using the JSON Subset Configuration

Dumping all of the Windows API produces a massive file. Instead, `c3-bindgen-win32api-from-winmn` takes a subset configuration file (like `data/win32.json`) to recursively resolve only the types, constants, and functions you actually need for your project.

### Example JSON Subset (`data/win32.json`):
```json
{
  "Windows.Win32.System.Memory": {
    "functions": {
      "include": [
        "*"
      ]
    },
    "types": {
      "include": [
        "*"
      ]
    },
    "constants": {
      "include": [
        "*"
      ],
      "exclude": [
        "MEM_REPLACE_*"
      ]
    }
  },
  "Windows.Win32.UI.WindowsAndMessaging": {
    "functions": {
      "include": [
        "CreateWindowExW",
        "DefWindowProcW",
        "RegisterClassExW"
      ]
    },
    "types": {
      "include": [
        "MSG",
        "WNDCLASSEXW"
      ]
    },
    "constants": {
      "include": [
        "WM_*",
        "WS_*"
      ]
    }
  }
}
```

### Key Configuration Fields:
1. Top-level keys are Win32 namespace names, such as `Windows.Win32.System.Memory`.
2. Each namespace can configure `functions`, `types`, and `constants`.
3. Each kind supports `include` and `exclude` glob filters. `include: ["*"]` allows all identifiers of that kind in the namespace; `exclude` wins and bans matching identifiers from generation.
4. If a kind is omitted, it is not seeded directly from that namespace. Dependencies of included functions/types are still resolved automatically.
5. A namespace can still define `typeNameOverrides` for one-off C3 type names, but common Win32 screaming-case names are projected automatically.
6. The emitted C3 module name defaults to `win32`.

### Dependency Resolution
You only need to list the main functions or structs you use. The generator's `DependencyResolver` automatically traverses parameters, struct fields, and return types, pulling in transitive dependencies recursively (e.g. including `RegisterClassExW` will automatically resolve and pull in `WndClassExW`, `WndProc`, `HINSTANCE`, etc.).

---

## Compiling & Using the Bindings in C3

The generated `.c3i` files act as C3 interface files. Each file declares the same C3 module and is split by Win32 namespace. You import the module name defined in your subset JSON:

```c3
import win32;
```

Compile your C3 files alongside every generated interface file using the C3 compiler:
```powershell
$bindings = Get-ChildItem out/win32 -Filter *.c3i | ForEach-Object FullName
c3c compile -o my_app @bindings my_app.c3
```

---

## Example GUI Application

A full OpenGL window-creation example is available in the `examples/` directory.

### Build and Run the Example:
Navigate to the `examples/` directory and execute the build batch script:
```powershell
cd examples
.\build.bat
```

The example demonstrates:
1. Allocating and initializing the `WndClassExW` struct in C3.
2. Registering the window class with `win32::register_class_exW`.
3. Creating a GUI window with OpenGL double-buffering context setup.
4. Processing the standard Win32 message loop using `win32::peek_messageW`.
