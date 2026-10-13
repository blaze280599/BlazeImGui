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

    /// <summary>Called once per frame by <see cref="BlazeOverlay"/>; call it yourself only if you do not use the base class.</summary>
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

    /// <summary>A short display name for a virtual-key code ("Insert", "F5", "Mouse 4", "None").</summary>
    public static string KeyName(int key)
    {
        if (key == 0) return "None";
        if (Enum.IsDefined(typeof(VirtualKey), key))
        {
            var name = ((VirtualKey)key).ToString();
            return name switch
            {
                "LButton" => "Mouse 1", "RButton" => "Mouse 2", "MButton" => "Mouse 3",
                "XButton1" => "Mouse 4", "XButton2" => "Mouse 5",
                "PageUp" => "Page Up", "PageDown" => "Page Down",
                "LShift" => "L Shift", "RShift" => "R Shift", "LControl" => "L Ctrl", "RControl" => "R Ctrl",
                "LAlt" => "L Alt", "RAlt" => "R Alt", "CapsLock" => "Caps Lock",
                _ when name.StartsWith("D") && name.Length == 2 => name[1..],
                _ when name.StartsWith("NumPad") => "Num " + name[6..],
                _ => name,
            };
        }
        return $"Key 0x{key:X2}";
    }

    // Shift/Ctrl/Alt report both a generic and a left/right code; the keybind widget keeps the specific one.
    private static bool IsModifierAlias(int key) => key is 0x10 or 0x11 or 0x12;

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
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
