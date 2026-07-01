using Microsoft.Data.Sqlite;
using WinmnDump.Generation;
using WinmnDump.Model;
using WinmnDump.Persistence;

namespace WinmnDump.Tests;

internal static class SelfTests
{
    public static void Run()
    {
        TestNameProjection();
        TestTypeMapping();
        TestResolverAndEmitter();
        TestMultiFileEmitter();
        TestBroadSeeding();
        TestDatabaseWriter();
        Console.Error.WriteLine("self-tests passed");
    }

    private static void TestNameProjection()
    {
        var names = new C3NameProjector();

        Equal("WndClassExW", names.TypeName("WNDCLASSEXW"));
        Equal("HWnd", names.TypeName("Windows.Win32.Foundation.HWND"));
        Equal("Hdc", names.TypeName("Windows.Win32.Graphics.Gdi.HDC"));
        Equal("Hglrc", names.TypeName("Windows.Win32.Graphics.OpenGL.HGLRC"));
        Equal("HSurf", names.TypeName("HSURF"));
        Equal("HSyntheticPointerDevice", names.TypeName("HSYNTHETICPOINTERDEVICE"));
        Equal("HWinEventHook", names.TypeName("HWINEVENTHOOK"));
        Equal("HWinsta", names.TypeName("HWINSTA"));
        Equal("LogColorSpaceW", names.TypeName("LOGCOLORSPACEW"));
        Equal("MenuItemInfoA", names.TypeName("MENUITEMINFOA"));
        Equal("PropEnumProcExW", names.TypeName("PROPENUMPROCEXW"));
        Equal("TtLoadEmbeddedFontStatus", names.TypeName("TTLOAD_EMBEDDED_FONT_STATUS"));
        Equal("UpdateLayeredWindowInfo", names.TypeName("UPDATELAYEREDWINDOWINFO"));
        Equal("WglSwap", names.TypeName("WGLSWAP"));
        Equal("XFormObj", names.TypeName("XFORMOBJ"));
        Equal("get_window_rect", names.FunctionName("GetWindowRect"));
        Equal("create_window_exA", names.FunctionName("CreateWindowExA"));
        Equal("create_window_exW", names.FunctionName("CreateWindowExW"));
        Equal("register_class_exW", names.FunctionName("RegisterClassExW"));
        Equal("get_dc", names.FunctionName("GetDC"));
        Equal("get_dc_ex", names.FunctionName("GetDCEx"));
        Equal("set_window_long_ptrW", names.FunctionName("SetWindowLongPtrW"));
        Equal("brushobj_hget_color_transform", names.FunctionName("BRUSHOBJ_hGetColorTransform"));
        Equal("clipobj_benum", names.FunctionName("CLIPOBJ_bEnum"));
        Equal("lpWndClass", names.ParameterName("lpWndClass"));
        Equal("fn_", names.ParameterName("fn"));
        Equal("WS_VISIBLE", names.ConstantName("WS_VISIBLE"));

        var customNames = new C3NameProjector(new Dictionary<string, string>
        {
            ["Windows.Win32.Foundation.HWND"] = "WindowHandle",
            ["HDC"] = "DeviceContext"
        });
        Equal("WindowHandle", customNames.TypeName("HWND"));
        Equal("DeviceContext", customNames.TypeName("Windows.Win32.Graphics.Gdi.HDC"));
    }

    private static void TestTypeMapping()
    {
        var mapper = new C3TypeMapper(new C3NameProjector());

        Equal("uint", mapper.Map("u32"));
        Equal("HWnd*", mapper.Map("Windows.Win32.Foundation.HWND*"));
        Equal("Hdc", mapper.Map("Windows.Win32.Graphics.Gdi.HDC"));
        Equal("ushort*", mapper.Map("Windows.Win32.Foundation.PCWSTR"));
        Equal("Rect*", mapper.Map("Windows.Win32.Foundation.RECT&"));
        Equal("char[32]", mapper.Map("U8[32]"));
        Equal("uint*", mapper.Map("U32[]"));
    }

    private static void TestResolverAndEmitter()
    {
        var api = new ApiDatabase();

        api.Types["BOOL"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "BOOL",
            Kind = ApiTypeKind.Alias,
            AliasTarget = "i32"
        };
        api.Types["HWND"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "HWND",
            Kind = ApiTypeKind.Handle,
            AliasTarget = "void*"
        };
        api.Types["RECT"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "RECT",
            Kind = ApiTypeKind.Struct
        };
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "left", Type = "i32" });
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "top", Type = "i32" });
        api.Types["WNDCLASSA"] = new ApiType
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WNDCLASSA",
            Kind = ApiTypeKind.Struct
        };
        api.Types["WNDCLASSA"].Fields.Add(new ApiField { OriginalName = "style", Type = "u32" });
        api.Types["WNDCLASSW"] = new ApiType
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WNDCLASSW",
            Kind = ApiTypeKind.Struct
        };
        api.Types["WNDCLASSW"].Fields.Add(new ApiField { OriginalName = "style", Type = "u32" });

        var fn = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "GetWindowRect",
            ReturnType = "Windows.Win32.Foundation.BOOL",
            ImportModule = "USER32.dll"
        };
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "hWnd",
            Type = "Windows.Win32.Foundation.HWND"
        });
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpRect",
            Type = "Windows.Win32.Foundation.RECT*",
            Direction = ParamDirection.Out
        });
        api.Functions["GetWindowRect"] = fn;

        var fnRegisterClass = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "RegisterClassA",
            ReturnType = "u16",
            ImportModule = "USER32.dll"
        };
        fnRegisterClass.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpWndClass",
            Type = "Windows.Win32.UI.WindowsAndMessaging.WNDCLASSA*",
            Direction = ParamDirection.None,
            Const = true,
            NonNull = true
        });
        fnRegisterClass.Parameters.Add(new ApiParameter
        {
            OriginalName = "hInstance",
            Type = "Windows.Win32.Foundation.HWND*",
            Direction = ParamDirection.In,
            Optional = true,
            NonNull = false
        });
        api.Functions["RegisterClassA"] = fnRegisterClass;

        var fnRegisterClassW = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "RegisterClassW",
            ReturnType = "u16",
            ImportModule = "USER32.dll"
        };
        fnRegisterClassW.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpWndClass",
            Type = "Windows.Win32.UI.WindowsAndMessaging.WNDCLASSW*",
            Direction = ParamDirection.None,
            Const = true,
            NonNull = true
        });
        fnRegisterClassW.Parameters.Add(new ApiParameter
        {
            OriginalName = "hInstance",
            Type = "Windows.Win32.Foundation.HWND*",
            Direction = ParamDirection.In,
            Optional = true,
            NonNull = false
        });
        api.Functions["RegisterClassW"] = fnRegisterClassW;

        api.Constants["WS_VISIBLE"] = new ApiConstant
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WS_VISIBLE",
            Type = "u32",
            Value = "268435456"
        };

        var spec = new SubsetSpec { Module = "win32" };
        spec.Namespaces["Windows.Win32.UI.WindowsAndMessaging"] = new NamespaceSpec
        {
            Functions = Filter(["GetWindowRect", "RegisterClassA", "RegisterClassW"]),
            Constants = Filter(["WS_VISIBLE"])
        };

        var resolved = new SubsetResolver().Resolve(api, spec);
        True(resolved.Types.Contains("BOOL"), "return type dependency was not resolved");
        True(resolved.Types.Contains("HWND"), "parameter type dependency was not resolved");
        True(resolved.Types.Contains("RECT"), "pointer parameter dependency was not resolved");

        var names = new C3NameProjector();
        var typeMapper = new C3TypeMapper(names);
        var binding = new GeneratedBindingBuilder(names, typeMapper).Build(api, resolved, "win32");
        var output = new C3Emitter().Emit(binding);

        Contains(output, "module win32;");
        Contains(output, "alias Bool = int;");
        Contains(output, "alias HWnd = void*;");
        Contains(output, "struct Rect");
        Contains(output, "const uint WS_VISIBLE = 268435456;");
        Contains(output, "@param [out] lpRect");
        Contains(output, "@param [&in] lpWndClass");
        Contains(output, "@param [in] hInstance");
        Contains(output, "extern fn Bool get_window_rect(HWnd hWnd, Rect* lpRect)");
        Contains(output, "@cname(\"GetWindowRect\") @link(\"user32\");");
        Contains(output, "macro ushort register_class(WndClassA* lpWndClass, HWnd* hInstance)\r\n@if(!$feature(WIN32_UNICODE))");
        Contains(output, "    return register_classA(lpWndClass, hInstance);");
        Contains(output, "macro ushort register_class(WndClassW* lpWndClass, HWnd* hInstance)\r\n@if($feature(WIN32_UNICODE))");
        Contains(output, "    return register_classW(lpWndClass, hInstance);");
    }

    private static void TestMultiFileEmitter()
    {
        var api = CreateWindowRectApi();
        api.Types["_Bits_e__Struct"] = new ApiType
        {
            Namespace = "",
            OriginalName = "_Bits_e__Struct",
            Kind = ApiTypeKind.Struct
        };
        api.Types["_Bits_e__Struct"].Fields.Add(new ApiField { OriginalName = "_bitfield", Type = "u32" });
        api.Types["WNDCLASSA"] = new ApiType
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WNDCLASSA",
            Kind = ApiTypeKind.Struct
        };
        api.Types["WNDCLASSA"].Fields.Add(new ApiField { OriginalName = "style", Type = "u32" });
        api.Types["WNDCLASSW"] = new ApiType
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WNDCLASSW",
            Kind = ApiTypeKind.Struct
        };
        api.Types["WNDCLASSW"].Fields.Add(new ApiField { OriginalName = "style", Type = "u32" });

        var fnRegisterClass = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "RegisterClassA",
            ReturnType = "u16",
            ImportModule = "USER32.dll"
        };
        fnRegisterClass.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpWndClass",
            Type = "Windows.Win32.UI.WindowsAndMessaging.WNDCLASSA*"
        });
        api.Functions["RegisterClassA"] = fnRegisterClass;

        var fnRegisterClassW = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "RegisterClassW",
            ReturnType = "u16",
            ImportModule = "USER32.dll"
        };
        fnRegisterClassW.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpWndClass",
            Type = "Windows.Win32.UI.WindowsAndMessaging.WNDCLASSW*"
        });
        api.Functions["RegisterClassW"] = fnRegisterClassW;

        var spec = new SubsetSpec { Module = "win32" };
        spec.Namespaces["Windows.Win32.UI.WindowsAndMessaging"] = new NamespaceSpec
        {
            Functions = Filter(["GetWindowRect", "RegisterClassA", "RegisterClassW"]),
            Constants = Filter(["WS_VISIBLE"])
        };
        spec.Namespaces[""] = new NamespaceSpec
        {
            Types = Filter(["_Bits_e__Struct"])
        };

        var resolved = new SubsetResolver().Resolve(api, spec);
        var names = new C3NameProjector();
        var types = new C3TypeMapper(names);
        var binding = new GeneratedBindingBuilder(names, types).Build(api, resolved, "win32");
        binding.Warnings.Add("sample warning");

        var files = new C3Emitter().EmitFiles(binding);

        True(files.ContainsKey("_shared.c3i"), "shared file was not emitted");
        True(files.ContainsKey("foundation.c3i"), "foundation namespace file was not emitted");
        True(files.ContainsKey("ui_windows_and_messaging.c3i"), "ui namespace file was not emitted");
        True(!files.ContainsKey("global.c3i"), "empty namespace file should not be emitted");

        Contains(files["_shared.c3i"], "module win32;");
        Contains(files["_shared.c3i"], "struct Guid");
        Contains(files["_shared.c3i"], "struct BitsEStruct");
        Contains(files["_shared.c3i"], "// warning: sample warning");

        Contains(files["foundation.c3i"], "module win32;");
        Contains(files["foundation.c3i"], "alias Bool = int;");
        Contains(files["foundation.c3i"], "struct Rect");
        DoesNotContain(files["foundation.c3i"], "@cname(\"GetWindowRect\")");

        Contains(files["ui_windows_and_messaging.c3i"], "module win32;");
        Contains(files["ui_windows_and_messaging.c3i"], "// Win32 namespace: Windows.Win32.UI.WindowsAndMessaging");
        Contains(files["ui_windows_and_messaging.c3i"], "struct WndClassA");
        Contains(files["ui_windows_and_messaging.c3i"], "struct WndClassW");
        Contains(files["ui_windows_and_messaging.c3i"], "const uint WS_VISIBLE = 268435456;");
        Contains(files["ui_windows_and_messaging.c3i"], "extern fn Bool get_window_rect(HWnd hWnd, Rect* lpRect)");
        Contains(files["ui_windows_and_messaging.c3i"], "extern fn ushort register_classA(WndClassA* lpWndClass)");
        Contains(files["ui_windows_and_messaging.c3i"], "macro ushort register_class(WndClassA* lpWndClass)\r\n@if(!$feature(WIN32_UNICODE))");
        Contains(files["ui_windows_and_messaging.c3i"], "macro ushort register_class(WndClassW* lpWndClass)\r\n@if($feature(WIN32_UNICODE))");
        DoesNotContain(files["ui_windows_and_messaging.c3i"], "alias Bool = int;");
    }

    private static void TestBroadSeeding()
    {
        var api = new ApiDatabase();
        api.Types["HWND"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "HWND",
            Kind = ApiTypeKind.Handle,
            AliasTarget = "void*"
        };
        api.Types["MEMORY_BASIC_INFORMATION"] = new ApiType
        {
            Namespace = "Windows.Win32.System.Memory",
            OriginalName = "MEMORY_BASIC_INFORMATION",
            Kind = ApiTypeKind.Struct
        };
        api.Types["Apis"] = new ApiType
        {
            Namespace = "Windows.Win32.System.Memory",
            OriginalName = "Apis",
            Kind = ApiTypeKind.Class
        };

        api.Functions["SwapBuffers"] = new ApiFunction
        {
            Namespace = "Windows.Win32.Graphics.Gdi",
            OriginalName = "SwapBuffers",
            ReturnType = "Windows.Win32.Foundation.BOOL",
            ImportModule = "GDI32.dll"
        };

        api.Functions["glClear"] = new ApiFunction
        {
            Namespace = "Windows.Win32.Graphics.OpenGL",
            OriginalName = "glClear",
            ReturnType = "void",
            ImportModule = "OPENGL32.dll"
        };

        api.Functions["VirtualAlloc"] = new ApiFunction
        {
            Namespace = "Windows.Win32.System.Memory",
            OriginalName = "VirtualAlloc",
            ReturnType = "void*",
            ImportModule = "KERNEL32.dll"
        };

        api.Constants["MEM_COMMIT"] = new ApiConstant
        {
            Namespace = "Windows.Win32.System.Memory",
            OriginalName = "MEM_COMMIT",
            Type = "u32",
            Value = "4096"
        };
        api.Constants["MEM_PRIVATE"] = new ApiConstant
        {
            Namespace = "Windows.Win32.System.Memory",
            OriginalName = "MEM_PRIVATE",
            Type = "u32",
            Value = "131072"
        };

        api.Constants["WM_CLOSE"] = new ApiConstant
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WM_CLOSE",
            Type = "u32",
            Value = "16"
        };

        api.Constants["UNRELATED"] = new ApiConstant
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "UNRELATED",
            Type = "u32",
            Value = "1"
        };

        var spec = new SubsetSpec();
        spec.Namespaces["Windows.Win32.Graphics.Gdi"] = new NamespaceSpec
        {
            Functions = Filter(["SwapBuffers"])
        };
        spec.Namespaces["Windows.Win32.Graphics.OpenGL"] = new NamespaceSpec
        {
            Functions = Filter(["*"])
        };
        spec.Namespaces["Windows.Win32.System.Memory"] = new NamespaceSpec
        {
            Functions = Filter(["*"]),
            Types = Filter(["*"]),
            Constants = Filter(["*"], ["MEM_PRIVATE"])
        };
        spec.Namespaces["Windows.Win32.UI.WindowsAndMessaging"] = new NamespaceSpec
        {
            Constants = Filter(["WM_*"])
        };

        var result = new SubsetResolver().Resolve(api, spec);

        True(result.Functions.Contains("glClear"), "namespace function seed was not resolved");
        True(result.Functions.Contains("VirtualAlloc"), "namespace memory function seed was not resolved");
        True(result.Types.Contains("MEMORY_BASIC_INFORMATION"), "namespace type seed was not resolved");
        True(!result.Types.Contains("Apis"), "namespace type seed included static API holder type");
        True(result.Constants.Contains("MEM_COMMIT"), "namespace constant seed was not resolved");
        True(!result.Constants.Contains("MEM_PRIVATE"), "namespace constant exclude did not ban generation");
        True(result.Functions.Contains("SwapBuffers"), "namespace function include seed was not resolved");
        True(result.Constants.Contains("WM_CLOSE"), "wildcard constant seed was not resolved");
        True(!result.Constants.Contains("UNRELATED"), "wildcard constant seed included unrelated constant");
    }

    private static void TestDatabaseWriter()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"bindgen-runs-{Guid.NewGuid():N}.sqlite");

        try
        {
            var api = CreateWindowRectApi();
            var spec = new SubsetSpec { Module = "win32" };
            spec.Namespaces["Windows.Win32.UI.WindowsAndMessaging"] = new NamespaceSpec
            {
                Functions = Filter(["GetWindowRect"]),
                Constants = Filter(["WS_VISIBLE"])
            };
            var resolved = new SubsetResolver().Resolve(api, spec);
            var names = new C3NameProjector();
            var types = new C3TypeMapper(names);
            var binding = new GeneratedBindingBuilder(names, types).Build(api, resolved, "win32");
            binding.Warnings.Add("sample warning");

            var writer = new BindgenDatabaseWriter();
            var context = new BindgenRunContext
            {
                WinmdPath = @"C:\Windows.Win32.winmd",
                SubsetPath = @"C:\subset.json",
                OutputPath = @"C:\out\win32",
                SubsetJson = "{\"module\":\"win32\"}",
                GeneratorVersion = "self-test"
            };

            var firstRunId = writer.WriteRun(dbPath, context, binding);
            var secondRunId = writer.WriteRun(dbPath, context, binding);

            True(secondRunId > firstRunId, "database run ids did not append");

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Pooling = false
            }.ToString());
            connection.Open();

            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM runs;"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM functions WHERE original_name = 'GetWindowRect' AND c3_name = 'get_window_rect';"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM function_parameters WHERE function_original_name = 'GetWindowRect' AND original_name = 'lpRect' AND c3_type = 'Rect*' AND direction = 'Out';"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM types WHERE original_name = 'RECT' AND c3_name = 'Rect' AND c3_decl_kind = 'struct' AND emitted = 1;"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM type_fields WHERE type_original_name = 'RECT' AND original_name = 'left' AND c3_type = 'int' AND emitted = 1;"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM constants WHERE original_name = 'WS_VISIBLE' AND c3_name = 'WS_VISIBLE' AND c3_type = 'uint';"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM link_libraries WHERE library = 'user32' AND source_import_modules = 'USER32.dll';"));
            Equal(2, ScalarInt(connection, "SELECT COUNT(*) FROM warnings WHERE message = 'sample warning';"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static ApiDatabase CreateWindowRectApi()
    {
        var api = new ApiDatabase();

        api.Types["BOOL"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "BOOL",
            Kind = ApiTypeKind.Alias,
            AliasTarget = "i32"
        };
        api.Types["HWND"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "HWND",
            Kind = ApiTypeKind.Handle,
            AliasTarget = "void*"
        };
        api.Types["RECT"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "RECT",
            Kind = ApiTypeKind.Struct
        };
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "left", Type = "i32" });
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "top", Type = "i32" });

        var fn = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "GetWindowRect",
            ReturnType = "Windows.Win32.Foundation.BOOL",
            ImportModule = "USER32.dll"
        };
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "hWnd",
            Type = "Windows.Win32.Foundation.HWND"
        });
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpRect",
            Type = "Windows.Win32.Foundation.RECT*",
            Direction = ParamDirection.Out
        });
        api.Functions["GetWindowRect"] = fn;

        api.Constants["WS_VISIBLE"] = new ApiConstant
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WS_VISIBLE",
            Type = "u32",
            Value = "268435456"
        };

        return api;
    }

    private static void Equal(string expected, string actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"expected '{expected}', got '{actual}'");
    }

    private static void Equal(int expected, int actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"expected '{expected}', got '{actual}'");
    }

    private static void True(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void Contains(string haystack, string needle)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
            throw new InvalidOperationException($"missing expected output: {needle}\n\noutput was:\n{haystack}");
    }

    private static void DoesNotContain(string haystack, string needle)
    {
        if (haystack.Contains(needle, StringComparison.Ordinal))
            throw new InvalidOperationException($"unexpected output: {needle}\n\noutput was:\n{haystack}");
    }

    private static int ScalarInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static IdentifierFilter Filter(string[] include, string[]? exclude = null)
    {
        var filter = new IdentifierFilter();
        filter.Include.AddRange(include);

        if (exclude is not null)
            filter.Exclude.AddRange(exclude);

        return filter;
    }
}
