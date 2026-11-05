using System.Runtime.InteropServices;

namespace BlazeImGui;

/// <summary>
/// Global keyboard/mouse state through GetAsyncKeyState, so hotkeys work while the overlay is click-through
/// and the game has focus. Keys are Windows virtual-key codes; <see cref="VirtualKey"/> names the common ones.
/// </summary>
public static class Input
{
    private static readonly bool[] Previous = new bool[256];
    private static readonly bool[] Current = new bool[256];

    /// <summary>Called once per frame by <c>BlazeOverlay</c>; call it yourself only if you do not use the base class.</summary>
    public static void Update()
    {
        for (var key = 1; key < 256; key++)
        {
            Previous[key] = Current[key];
            Current[key] = (GetAsyncKeyState(key) & 0x8000) != 0;
        }
    }

    public static bool IsDown(int key) => key is > 0 and < 256 && Current[key];

    /// <summary>True only on the frame the key went down.</summary>
    public static bool Pressed(int key) => key is > 0 and < 256 && Current[key] && !Previous[key];

    public static bool Released(int key) => key is > 0 and < 256 && !Current[key] && Previous[key];

    /// <summary>The first key pressed this frame, or 0. Used by the keybind widget.</summary>
    public static int AnyPressed()
    {
        for (var key = 1; key < 256; key++)
            if (Current[key] && !Previous[key] && !IsModifierAlias(key)) return key;
        return 0;
    }

    /// <summary>
    /// A short display name for a virtual-key code ("Insert", "F5", "Mouse 4", "Num /", "None"). Punctuation keys show
    /// the character they type on the current keyboard layout ("-", "/", "]" on US), with the US character as fallback.
    /// </summary>
    public static string KeyName(int key)
    {
        if (key == 0) return "None";
        if (Names.TryGetValue(key, out var name)) return name;
        if (key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A) return ((char)key).ToString();
        if (key is >= 0x60 and <= 0x69) return $"Num {key - 0x60}";
        if (key is >= 0x70 and <= 0x87) return $"F{key - 0x6F}";
        if (Punctuation.TryGetValue(key, out var us))
        {
            // The layout's unshifted character; the high bit marks a dead key, which still names the key.
            var typed = (char)(MapVirtualKeyW((uint)key, 2) & 0x7FFF);
            return typed > ' ' && typed != (char)0x7F ? char.ToUpperInvariant(typed).ToString() : us;
        }
        return $"Key 0x{key:X2}";
    }

    private static readonly Dictionary<int, string> Names = new()
    {
        [0x01] = "Mouse 1", [0x02] = "Mouse 2", [0x04] = "Mouse 3", [0x05] = "Mouse 4", [0x06] = "Mouse 5",
        [0x08] = "Backspace", [0x09] = "Tab", [0x0C] = "Clear", [0x0D] = "Enter", [0x10] = "Shift", [0x11] = "Control",
        [0x12] = "Alt", [0x13] = "Pause", [0x14] = "Caps Lock", [0x1B] = "Escape", [0x20] = "Space", [0x21] = "Page Up",
        [0x22] = "Page Down", [0x23] = "End", [0x24] = "Home", [0x25] = "Left", [0x26] = "Up", [0x27] = "Right",
        [0x28] = "Down", [0x2C] = "Print Screen", [0x2D] = "Insert", [0x2E] = "Delete", [0x5B] = "L Win", [0x5C] = "R Win",
        [0x5D] = "Menu", [0x6A] = "Num *", [0x6B] = "Num +", [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
        [0x90] = "Num Lock", [0x91] = "Scroll Lock", [0xA0] = "L Shift", [0xA1] = "R Shift", [0xA2] = "L Ctrl",
        [0xA3] = "R Ctrl", [0xA4] = "L Alt", [0xA5] = "R Alt", [0xA6] = "Browser Back", [0xA7] = "Browser Forward",
        [0xA8] = "Browser Refresh", [0xA9] = "Browser Stop", [0xAA] = "Browser Search", [0xAB] = "Browser Favorites",
        [0xAC] = "Browser Home", [0xAD] = "Volume Mute", [0xAE] = "Volume Down", [0xAF] = "Volume Up",
        [0xB0] = "Next Track", [0xB1] = "Previous Track", [0xB2] = "Stop Media", [0xB3] = "Play/Pause", [0xB4] = "Mail",
        [0xB5] = "Media Select", [0xB6] = "App 1", [0xB7] = "App 2",
    };

    // OEM keys and the character each types on a US layout.
    private static readonly Dictionary<int, string> Punctuation = new()
    {
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/", [0xC0] = "`",
        [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'", [0xDF] = "OEM 8", [0xE2] = "\\",
    };

    // Shift/Ctrl/Alt report both a generic and a left/right code; the keybind widget keeps the specific one.
    private static bool IsModifierAlias(int key) => key is 0x10 or 0x11 or 0x12;

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint MapVirtualKeyW(uint code, uint mapType);
}

/// <summary>Common Windows virtual-key codes. Cast to int where an API takes a key: <c>(int)VirtualKey.Insert</c>.</summary>
public enum VirtualKey
{
    None = 0, LButton = 0x01, RButton = 0x02, MButton = 0x04, XButton1 = 0x05, XButton2 = 0x06,
    Back = 0x08, Tab = 0x09, Enter = 0x0D, Shift = 0x10, Control = 0x11, Alt = 0x12, Pause = 0x13, CapsLock = 0x14,
    Escape = 0x1B, Space = 0x20, PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24,
    Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, Insert = 0x2D, Delete = 0x2E,
    D0 = 0x30, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A = 0x41, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    NumPad0 = 0x60, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    F1 = 0x70, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    LShift = 0xA0, RShift = 0xA1, LControl = 0xA2, RControl = 0xA3, LAlt = 0xA4, RAlt = 0xA5,
}
