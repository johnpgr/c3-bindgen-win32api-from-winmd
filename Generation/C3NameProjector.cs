using System.Text;

namespace WinmnDump.Generation;

public sealed class C3NameProjector
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "alias",
        "asm",
        "attrdef",
        "bitstruct",
        "break",
        "case",
        "catch",
        "const",
        "continue",
        "default",
        "defer",
        "distinct",
        "do",
        "else",
        "enum",
        "extern",
        "false",
        "fault",
        "fn",
        "foreach",
        "for",
        "if",
        "import",
        "interface",
        "macro",
        "module",
        "nextcase",
        "null",
        "return",
        "struct",
        "switch",
        "tlocal",
        "true",
        "try",
        "typedef",
        "union",
        "var",
        "while"
    };

    private readonly Dictionary<string, string> _typeOverrides = new(StringComparer.Ordinal)
    {
        ["ATOM"] = "Atom",
        ["BOOL"] = "Bool",
        ["COLORREF"] = "ColorRef",
        ["DWORD"] = "DWord",
        ["HANDLE"] = "Handle",
        ["HBRUSH"] = "HBrush",
        ["HCURSOR"] = "HCursor",
        ["HDC"] = "Hdc",
        ["HGLRC"] = "Hglrc",
        ["HICON"] = "HIcon",
        ["HINSTANCE"] = "HInstance",
        ["HMENU"] = "HMenu",
        ["HMODULE"] = "HModule",
        ["HWND"] = "HWnd",
        ["LPARAM"] = "LParam",
        ["LPCSTR"] = "LpcStr",
        ["LPCWSTR"] = "LpcWStr",
        ["LPSTR"] = "LpStr",
        ["LPWSTR"] = "LpWStr",
        ["LRESULT"] = "LResult",
        ["MSG"] = "Msg",
        ["POINT"] = "Point",
        ["RECT"] = "Rect",
        ["UINT"] = "UInt",
        ["ULONG_PTR"] = "ULongPtr",
        ["WNDCLASS_STYLES"] = "WndClassStyles",
        ["WNDCLASSEXA"] = "WndClassExA",
        ["WNDCLASSEXW"] = "WndClassExW",
        ["WNDCLASSA"] = "WndClassA",
        ["WNDCLASSW"] = "WndClassW",
        ["WNDPROC"] = "WndProc",
        ["WPARAM"] = "WParam"
    };

    private static readonly Dictionary<string, string> TypeNameWords = new(StringComparer.Ordinal)
    {
        ["ABC"] = "Abc",
        ["ABORT"] = "Abort",
        ["ACCEL"] = "Accel",
        ["ADJUSTMENT"] = "Adjustment",
        ["ALLOC"] = "Alloc",
        ["ALTTAB"] = "AltTab",
        ["ASYNC"] = "Async",
        ["ATTR"] = "Attr",
        ["BACKGROUND"] = "Background",
        ["BAR"] = "Bar",
        ["BITMAP"] = "Bitmap",
        ["BITS"] = "Bits",
        ["BLEND"] = "Blend",
        ["BOX"] = "Box",
        ["BRUSH"] = "Brush",
        ["BSM"] = "Bsm",
        ["BUTTON"] = "Button",
        ["CALLBACK"] = "Callback",
        ["CFP"] = "Cfp",
        ["CHANGE"] = "Change",
        ["CHARSET"] = "Charset",
        ["CIE"] = "Cie",
        ["CLIP"] = "Clip",
        ["COLORSPACE"] = "ColorSpace",
        ["COLOR"] = "Color",
        ["COMBOBOX"] = "ComboBox",
        ["CONFIG"] = "Config",
        ["CONSOLE"] = "Console",
        ["CONSTANTS"] = "Constants",
        ["CONTEXT"] = "Context",
        ["CONTROL"] = "Control",
        ["CONV"] = "Conv",
        ["CONVERSATION"] = "Conversation",
        ["CS"] = "Cs",
        ["CURSOR"] = "Cursor",
        ["DATA"] = "Data",
        ["DATE"] = "Date",
        ["DDE"] = "Dde",
        ["DESCRIPTOR"] = "Descriptor",
        ["DESIGN"] = "Design",
        ["DESK"] = "Desk",
        ["DESKTOP"] = "Desktop",
        ["DEVICE"] = "Device",
        ["DEV"] = "Dev",
        ["DH"] = "Dh",
        ["DISPLAY"] = "Display",
        ["DLG"] = "Dlg",
        ["DDA"] = "Dda",
        ["DOC"] = "Doc",
        ["DRAW"] = "Draw",
        ["DWP"] = "Dwp",
        ["DV"] = "Dv",
        ["EMBEDDED"] = "Embedded",
        ["EMBED"] = "Embed",
        ["EMF"] = "Emf",
        ["ENH"] = "Enh",
        ["ENTRY"] = "Entry",
        ["ENUM"] = "Enum",
        ["EVENT"] = "Event",
        ["EX"] = "Ex",
        ["FAR"] = "Far",
        ["FD"] = "Fd",
        ["FILE"] = "File",
        ["FILTER"] = "Filter",
        ["FLAGS"] = "Flags",
        ["FLASH"] = "Flash",
        ["FLOAT"] = "Float",
        ["FONT"] = "Font",
        ["FORMAT"] = "Format",
        ["FREE"] = "Free",
        ["FUNCTION"] = "Function",
        ["FX"] = "Fx",
        ["GCP"] = "Gcp",
        ["GDI"] = "Gdi",
        ["GESTURE"] = "Gesture",
        ["GLYPH"] = "Glyph",
        ["GLOBAL"] = "Global",
        ["GOBJ"] = "GObj",
        ["GRAY"] = "Gray",
        ["GUI"] = "Gui",
        ["H"] = "H",
        ["HANDLE"] = "Handle",
        ["HEADER"] = "Header",
        ["HELP"] = "Help",
        ["HOOK"] = "Hook",
        ["ICM"] = "Icm",
        ["ICON"] = "Icon",
        ["ID"] = "Id",
        ["IFI"] = "Ifi",
        ["IME"] = "Ime",
        ["INFO"] = "Info",
        ["INPUT"] = "Input",
        ["ITEM"] = "Item",
        ["KERNING"] = "Kerning",
        ["L"] = "L",
        ["LANG"] = "Lang",
        ["LAST"] = "Last",
        ["LAYERED"] = "Layered",
        ["LAYER"] = "Layer",
        ["LCS"] = "Lcs",
        ["LINE"] = "Line",
        ["LIST"] = "List",
        ["LOAD"] = "Load",
        ["LOCAL"] = "Local",
        ["LOG"] = "Log",
        ["MASK"] = "Mask",
        ["MENU"] = "Menu",
        ["META"] = "Meta",
        ["METAFILE"] = "Metafile",
        ["METRIC"] = "Metric",
        ["METRICS"] = "Metrics",
        ["MF"] = "Mf",
        ["MODE"] = "Mode",
        ["MONITOR"] = "Monitor",
        ["MOUSE"] = "Mouse",
        ["MOVE"] = "Move",
        ["MSG"] = "Msg",
        ["NAME"] = "Name",
        ["NOTIFY"] = "Notify",
        ["OBJ"] = "Obj",
        ["ORDERING"] = "Ordering",
        ["OUTLINE"] = "Outline",
        ["OUTPUT"] = "Output",
        ["PAIR"] = "Pair",
        ["PAINT"] = "Paint",
        ["PALETTE"] = "Palette",
        ["PARAMS"] = "Params",
        ["PATH"] = "Path",
        ["PDEV"] = "PDev",
        ["PEN"] = "Pen",
        ["PFN"] = "Pfn",
        ["PICT"] = "Pict",
        ["PIXEL"] = "Pixel",
        ["PLANE"] = "Plane",
        ["POINT"] = "Point",
        ["POINTER"] = "Pointer",
        ["POLY"] = "Poly",
        ["POWER"] = "Power",
        ["PROC"] = "Proc",
        ["PRO"] = "Pro",
        ["PROP"] = "Prop",
        ["P"] = "P",
        ["QF"] = "Qf",
        ["QUAD"] = "Quad",
        ["RANGE"] = "Range",
        ["RATIONAL"] = "Rational",
        ["RAW"] = "Raw",
        ["READ"] = "Read",
        ["REALLOC"] = "Realloc",
        ["RECORD"] = "Record",
        ["RECT"] = "Rect",
        ["RESULTS"] = "Results",
        ["RGN"] = "Rgn",
        ["RGB"] = "Rgb",
        ["ROTATION"] = "Rotation",
        ["RSRC"] = "Rsrc",
        ["RUN"] = "Run",
        ["SCALING"] = "Scaling",
        ["SCANLINE"] = "Scanline",
        ["SCROLL"] = "Scroll",
        ["SECURITY"] = "Security",
        ["SEMAPHORE"] = "Semaphore",
        ["SEND"] = "Send",
        ["SET"] = "Set",
        ["SIGNATURE"] = "Signature",
        ["SOURCE"] = "Source",
        ["STATUS"] = "Status",
        ["STATES"] = "States",
        ["STATE"] = "State",
        ["STRING"] = "String",
        ["STRUCT"] = "Struct",
        ["STR"] = "Str",
        ["STYPE"] = "Type",
        ["SURF"] = "Surf",
        ["SWAP"] = "Swap",
        ["SYNTHETIC"] = "Synthetic",
        ["TABLE"] = "Table",
        ["TARGET"] = "Target",
        ["TECHNOLOGY"] = "Technology",
        ["TESTS"] = "Tests",
        ["TEXT"] = "Text",
        ["THREAD"] = "Thread",
        ["TIME"] = "Time",
        ["TIMER"] = "Timer",
        ["TITLE"] = "Title",
        ["TOPOLOGY"] = "Topology",
        ["TOUCH"] = "Touch",
        ["TPM"] = "Tpm",
        ["TRACK"] = "Track",
        ["TRIPLE"] = "Triple",
        ["TRI"] = "Tri",
        ["TT"] = "Tt",
        ["TYPE"] = "Type",
        ["UPDATE"] = "Update",
        ["VALIDATION"] = "Validation",
        ["VECTOR"] = "Vector",
        ["VERTEX"] = "Vertex",
        ["VIDEO"] = "Video",
        ["WC"] = "Wc",
        ["WGL"] = "Wgl",
        ["WIN"] = "Win",
        ["WINDOW"] = "Window",
        ["WINEVENT"] = "WinEvent",
        ["WINSTA"] = "Winsta",
        ["WND"] = "Wnd",
        ["WRITE"] = "Write",
        ["W"] = "W",
        ["XFORM"] = "XForm",
        ["XLATE"] = "Xlate",
        ["XYZ"] = "Xyz"
    };

    private static readonly string[] TypeNameWordKeys = TypeNameWords.Keys
        .OrderByDescending(key => key.Length)
        .ToArray();

    private readonly Dictionary<string, string> _originalToC3Type = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _c3TypeToOriginal = new(StringComparer.Ordinal);

    public C3NameProjector(IReadOnlyDictionary<string, string>? typeNameOverrides = null)
    {
        if (typeNameOverrides is null)
            return;

        foreach (var (original, c3Name) in typeNameOverrides)
        {
            var key = LastNameSegment(original);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(c3Name))
                continue;

            _typeOverrides[key] = SanitizeTypeName(c3Name);
        }
    }

    public string TypeName(string original)
    {
        original = LastNameSegment(original);

        if (_originalToC3Type.TryGetValue(original, out var existing))
            return existing;

        var projected = _typeOverrides.TryGetValue(original, out var value)
            ? value
            : FallbackTypeName(original);

        RegisterTypeName(original, projected);
        return projected;
    }

    public string FunctionName(string original)
    {
        var name = LastNameSegment(original);
        name = SanitizeIdentifier(name, "item");

        var suffix = "";
        if (name.Length > 1 && name[^1] is 'A' or 'W')
        {
            suffix = name[^1].ToString();
            name = name[..^1];
        }

        var words = name
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitFunctionNameSegment)
            .Select(word => word.ToLowerInvariant())
            .ToList();

        var projected = words.Count == 0 ? "item" : string.Join("_", words);
        projected += suffix;
        return Keywords.Contains(projected) ? projected + "_" : projected;
    }

    public string FieldName(string original) => LowerFirst(SanitizeIdentifier(original, "field"));

    public string ParameterName(string original) => LowerFirst(SanitizeIdentifier(original, "param"));

    public string ConstantName(string original)
    {
        var name = SanitizeIdentifier(original, "constant");
        return name.ToUpperInvariant();
    }

    private void RegisterTypeName(string original, string projected)
    {
        if (_c3TypeToOriginal.TryGetValue(projected, out var otherOriginal) && otherOriginal != original)
        {
            throw new InvalidOperationException(
                $"C3 type name collision: {original} and {otherOriginal} both project to {projected}");
        }

        _originalToC3Type[original] = projected;
        _c3TypeToOriginal[projected] = original;
    }

    private static string FallbackTypeName(string original)
    {
        var parts = original
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ProjectTypeNamePart);

        var name = string.Concat(parts);
        if (string.IsNullOrEmpty(name))
            name = "Win32Type";

        if (!char.IsUpper(name[0]))
            name = char.ToUpperInvariant(name[0]) + name[1..];

        if (!name.Any(char.IsLower))
            name += "Type";

        return name;
    }

    private static string ProjectTypeNamePart(string value)
    {
        if (value.Length == 0)
            return value;

        if (!value.All(char.IsAsciiLetterOrDigit))
            return ToPascalPart(value);

        if (value.All(ch => char.IsAsciiLetterUpper(ch) || char.IsAsciiDigit(ch)) &&
            TrySplitUppercaseTypeName(value, out var projected))
        {
            return projected;
        }

        return ToPascalPart(value);
    }

    private static bool TrySplitUppercaseTypeName(string value, out string projected)
    {
        if (TypeNameWords.TryGetValue(value, out projected!))
            return true;

        if (value.Length > 1 && value[^1] is 'A' or 'W')
        {
            if (TrySplitUppercaseTypeName(value[..^1], out var baseName))
            {
                projected = baseName + value[^1];
                return true;
            }
        }

        var sb = new StringBuilder();
        var index = 0;

        while (index < value.Length)
        {
            var matched = false;
            foreach (var key in TypeNameWordKeys)
            {
                if (!value[index..].StartsWith(key, StringComparison.Ordinal))
                    continue;

                sb.Append(TypeNameWords[key]);
                index += key.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                projected = "";
                return false;
            }
        }

        projected = sb.ToString();
        return projected.Length > 0;
    }

    private static string SanitizeTypeName(string value)
    {
        var sanitized = SanitizeIdentifier(value, "Win32Type");
        if (!char.IsUpper(sanitized[0]))
            sanitized = char.ToUpperInvariant(sanitized[0]) + sanitized[1..];
        return sanitized;
    }

    private static string ToPascalPart(string value)
    {
        if (value.Length == 0)
            return value;

        if (value.All(char.IsUpper))
            value = value.ToLowerInvariant();

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static IEnumerable<string> SplitFunctionNameSegment(string value)
    {
        if (value.Length == 0)
            yield break;

        if (value.Length > 1 && char.IsAsciiLetterLower(value[0]) && char.IsAsciiLetterUpper(value[1]))
        {
            var rest = SplitFunctionNameSegment(value[1..]).ToList();
            if (rest.Count == 0)
            {
                yield return value;
                yield break;
            }

            yield return value[0] + rest[0];
            foreach (var word in rest.Skip(1))
                yield return word;
            yield break;
        }

        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            if (!IsFunctionNameBoundary(value, i))
                continue;

            yield return value[start..i];
            start = i;
        }

        yield return value[start..];
    }

    private static bool IsFunctionNameBoundary(string value, int index)
    {
        var previous = value[index - 1];
        var current = value[index];
        var next = index + 1 < value.Length ? value[index + 1] : '\0';

        if (char.IsAsciiLetterUpper(current))
        {
            return char.IsAsciiLetterLower(previous) ||
                char.IsAsciiDigit(previous) ||
                (char.IsAsciiLetterUpper(previous) && next != '\0' && char.IsAsciiLetterLower(next));
        }

        if (char.IsAsciiDigit(current))
            return !char.IsAsciiDigit(previous);

        return char.IsAsciiDigit(previous);
    }

    private static string LowerFirst(string value)
    {
        value = SanitizeIdentifier(value, "item");
        var lowercased = char.ToLowerInvariant(value[0]) + value[1..];
        return Keywords.Contains(lowercased) ? lowercased + "_" : lowercased;
    }

    private static string SanitizeIdentifier(string value, string fallback)
    {
        value = LastNameSegment(value);
        var sb = new StringBuilder();

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '_')
                sb.Append(ch);
        }

        if (sb.Length == 0)
            sb.Append(fallback);

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        var identifier = sb.ToString();
        return Keywords.Contains(identifier) ? identifier + "_" : identifier;
    }

    private static string LastNameSegment(string value)
    {
        var genericIndex = value.IndexOf('<', StringComparison.Ordinal);
        if (genericIndex >= 0)
            value = value[..genericIndex];

        var index = value.LastIndexOf('.');
        return index >= 0 ? value[(index + 1)..] : value;
    }
}
