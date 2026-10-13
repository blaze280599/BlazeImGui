using System.Runtime.InteropServices;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>
/// The embedded Inter typeface (Regular, Medium, SemiBold; Bold via <see cref="Add"/>) with the Lucide icon font merged into every size,
/// so icons can be mixed into any string: <c>ImGui.Text($"{Icons.Info}  Hello")</c>.
/// Switch fonts with <c>ImGui.PushFont(Fonts.SemiBold)</c> / <c>ImGui.PopFont()</c>.
/// </summary>
public static unsafe class Fonts
{
    /// <summary>Default body font (the ImGui default font).</summary>
    public static ImFontPtr Regular { get; private set; }
    /// <summary>Body size, medium weight: labels that need a little emphasis.</summary>
    public static ImFontPtr Medium { get; private set; }
    /// <summary>Body size, semibold: titles of cards and sections.</summary>
    public static ImFontPtr SemiBold { get; private set; }
    /// <summary>Two pixels smaller than body: captions, hints, badges.</summary>
    public static ImFontPtr Small { get; private set; }
    /// <summary>Small semibold: section labels.</summary>
    public static ImFontPtr SmallSemiBold { get; private set; }
    /// <summary>Large semibold: page titles.</summary>
    public static ImFontPtr Heading { get; private set; }
    /// <summary>Icons only, large: brand marks and empty states.</summary>
    public static ImFontPtr IconLarge { get; private set; }

    /// <summary>The body font size in pixels after scale.</summary>
    public static float BodySize { get; private set; }

    private static IntPtr _textRanges, _iconRanges;
    private static byte[]? _regular, _medium, _semibold, _bold, _icons;

    /// <summary>
    /// Adds every font to the atlas. Called from the overlay's font callback (<c>ReplaceFont</c>);
    /// use it directly only when you manage ImGui yourself.
    /// </summary>
    public static void Load(float bodySize = 15f, float scale = 1f)
    {
        var io = ImGui.GetIO();
        EnsureRanges();
        var regular = _regular ??= Resource("Inter-Regular.ttf");
        var medium = _medium ??= Resource("Inter-Medium.ttf");
        var semibold = _semibold ??= Resource("Inter-SemiBold.ttf");
        var icons = _icons ??= Resource("lucide.ttf");

        var body = MathF.Round(bodySize * scale);
        BodySize = body;
        // The first font added becomes the ImGui default font.
        Regular = AddText(regular, icons, body);
        Medium = AddText(medium, icons, body);
        SemiBold = AddText(semibold, icons, body);
        Small = AddText(regular, icons, body - 2);
        SmallSemiBold = AddText(semibold, icons, body - 2);
        Heading = AddText(semibold, icons, MathF.Round(body * 1.34f));
        IconLarge = AddIcons(icons, MathF.Round(body * 1.4f), merge: false);
        io.NativePtr->FontDefault = Regular.NativePtr;
    }

    /// <summary>
    /// Adds another Inter size/weight (icons merged) and returns it. Only valid while fonts are being built,
    /// i.e. inside <see cref="BlazeOverlay"/>'s <c>OnLoadFonts()</c> override. Store the result in a field:
    /// <code>protected override void OnLoadFonts() => _labelFont = Fonts.Add(FontWeight.SemiBold, 18);</code>
    /// <paramref name="size"/> is in pixels before UI scale.
    /// </summary>
    public static ImFontPtr Add(FontWeight weight, float size)
    {
        EnsureRanges();
        var text = weight switch
        {
            FontWeight.Medium => _medium ??= Resource("Inter-Medium.ttf"),
            FontWeight.SemiBold => _semibold ??= Resource("Inter-SemiBold.ttf"),
            FontWeight.Bold => _bold ??= Resource("Inter-Bold.ttf"),
            _ => _regular ??= Resource("Inter-Regular.ttf"),
        };
        return AddText(text, _icons ??= Resource("lucide.ttf"), MathF.Round(size * Ui.Scale));
    }

    private static ImFontPtr AddText(byte[] text, byte[] icons, float size)
    {
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->OversampleH = 2;
        config->OversampleV = 1;
        config->PixelSnapH = 1;
        var font = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(Copy(text), text.Length, size, config, _textRanges);
        ImGuiNative.ImFontConfig_destroy(config);
        AddIcons(icons, size, merge: true);
        return font;
    }

    private static ImFontPtr AddIcons(byte[] icons, float size, bool merge)
    {
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->MergeMode = merge ? (byte)1 : (byte)0;
        config->OversampleH = 1;
        config->OversampleV = 1;
        config->PixelSnapH = 1;
        // Icons are square: a fixed advance keeps icon columns aligned, and a small downward
        // offset centers the glyph on the text's x-height instead of sitting on the baseline.
        config->GlyphMinAdvanceX = size;
        config->GlyphOffset = new System.Numerics.Vector2(0, MathF.Round(size * .14f));
        var font = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(Copy(icons), icons.Length, size, config, _iconRanges);
        ImGuiNative.ImFontConfig_destroy(config);
        return font;
    }

    // ImGui frees font data it owns with its own allocator, so the bytes are copied into ImGui memory.
    private static IntPtr Copy(byte[] data)
    {
        var memory = ImGui.MemAlloc((uint)data.Length);
        Marshal.Copy(data, 0, memory, data.Length);
        return memory;
    }

    // Glyph ranges must outlive the atlas, so they are allocated once and never freed.
    private static void EnsureRanges()
    {
        if (_textRanges != IntPtr.Zero) return;
        _textRanges = Ranges(
            0x0020, 0x00FF, // Basic Latin + Latin-1
            0x0100, 0x017F, // Latin Extended-A
            0x2000, 0x206F, // General punctuation (– — … • ‘ ’ “ ”)
            0x2190, 0x21FF, // Arrows
            0x2200, 0x22FF); // Math operators (± × ≈ ≤ ≥)
        _iconRanges = Ranges(BlazeImGui.Icons.RangeStart, BlazeImGui.Icons.RangeEnd);
    }

    private static IntPtr Ranges(params ushort[] pairs)
    {
        var memory = Marshal.AllocHGlobal((pairs.Length + 1) * sizeof(ushort));
        var span = new Span<ushort>((void*)memory, pairs.Length + 1);
        pairs.CopyTo(span);
        span[^1] = 0;
        return memory;
    }

    private static byte[] Resource(string file)
    {
        using var stream = typeof(Fonts).Assembly.GetManifestResourceStream("BlazeImGui.Fonts." + file)
            ?? throw new InvalidOperationException($"Embedded font {file} is missing from BlazeImGui.dll.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

/// <summary>Inter weights embedded in the library.</summary>
public enum FontWeight { Regular, Medium, SemiBold, Bold }
