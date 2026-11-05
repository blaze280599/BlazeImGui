//! Global key state through `GetAsyncKeyState` (Windows virtual-key codes). Elsewhere every key reads as up.

use std::cell::Cell;

thread_local! {
    static KEYS: Cell<([bool; 256], [bool; 256])> = const { Cell::new(([false; 256], [false; 256])) };
}

#[cfg(windows)]
#[link(name = "user32")]
unsafe extern "system" {
    fn GetAsyncKeyState(key: i32) -> i16;
    fn MapVirtualKeyW(code: u32, map_type: u32) -> u32;
}

fn sample(key: usize) -> bool {
    #[cfg(windows)]
    {
        unsafe { (GetAsyncKeyState(key as i32) as u16 & 0x8000) != 0 }
    }
    #[cfg(not(windows))]
    {
        let _ = key;
        false
    }
}

/// Once per frame (`Overlay::render` calls it).
pub fn update() {
    let (_, current) = KEYS.get();
    let mut next = [false; 256];
    for (key, down) in next.iter_mut().enumerate().skip(1) {
        *down = sample(key);
    }
    KEYS.set((current, next));
}

fn state(key: i32) -> (bool, bool) {
    if !(1..256).contains(&key) {
        return (false, false);
    }
    let (previous, current) = KEYS.get();
    (previous[key as usize], current[key as usize])
}

pub fn is_down(key: i32) -> bool {
    state(key).1
}

/// Only on the frame it went down.
pub fn pressed(key: i32) -> bool {
    let (previous, current) = state(key);
    current && !previous
}

pub fn released(key: i32) -> bool {
    let (previous, current) = state(key);
    !current && previous
}

/// The first key pressed this frame, or 0 (Shift/Ctrl/Alt report their left/right codes).
pub fn any_pressed() -> i32 {
    (1..256).filter(|&key| !(0x10..=0x12).contains(&key)).find(|&key| pressed(key)).unwrap_or(0)
}

/// "Insert", "F5", "Mouse 4", "Num /", "None". Punctuation keys show the character they type on the current
/// keyboard layout ("-", "/", "]" on US), with the US character as fallback.
pub fn key_name(key: i32) -> String {
    const NAMES: &[(i32, &str)] = &[
        (0x01, "Mouse 1"),
        (0x02, "Mouse 2"),
        (0x04, "Mouse 3"),
        (0x05, "Mouse 4"),
        (0x06, "Mouse 5"),
        (0x08, "Backspace"),
        (0x09, "Tab"),
        (0x0C, "Clear"),
        (0x0D, "Enter"),
        (0x10, "Shift"),
        (0x11, "Control"),
        (0x12, "Alt"),
        (0x13, "Pause"),
        (0x14, "Caps Lock"),
        (0x1B, "Escape"),
        (0x20, "Space"),
        (0x21, "Page Up"),
        (0x22, "Page Down"),
        (0x23, "End"),
        (0x24, "Home"),
        (0x25, "Left"),
        (0x26, "Up"),
        (0x27, "Right"),
        (0x28, "Down"),
        (0x2C, "Print Screen"),
        (0x2D, "Insert"),
        (0x2E, "Delete"),
        (0x5B, "L Win"),
        (0x5C, "R Win"),
        (0x5D, "Menu"),
        (0x6A, "Num *"),
        (0x6B, "Num +"),
        (0x6D, "Num -"),
        (0x6E, "Num ."),
        (0x6F, "Num /"),
        (0x90, "Num Lock"),
        (0x91, "Scroll Lock"),
        (0xA0, "L Shift"),
        (0xA1, "R Shift"),
        (0xA2, "L Ctrl"),
        (0xA3, "R Ctrl"),
        (0xA4, "L Alt"),
        (0xA5, "R Alt"),
        (0xA6, "Browser Back"),
        (0xA7, "Browser Forward"),
        (0xA8, "Browser Refresh"),
        (0xA9, "Browser Stop"),
        (0xAA, "Browser Search"),
        (0xAB, "Browser Favorites"),
        (0xAC, "Browser Home"),
        (0xAD, "Volume Mute"),
        (0xAE, "Volume Down"),
        (0xAF, "Volume Up"),
        (0xB0, "Next Track"),
        (0xB1, "Previous Track"),
        (0xB2, "Stop Media"),
        (0xB3, "Play/Pause"),
        (0xB4, "Mail"),
        (0xB5, "Media Select"),
        (0xB6, "App 1"),
        (0xB7, "App 2"),
    ];
    // OEM keys and the character each types on a US layout.
    const PUNCTUATION: &[(i32, &str)] = &[
        (0xBA, ";"),
        (0xBB, "="),
        (0xBC, ","),
        (0xBD, "-"),
        (0xBE, "."),
        (0xBF, "/"),
        (0xC0, "`"),
        (0xDB, "["),
        (0xDC, "\\"),
        (0xDD, "]"),
        (0xDE, "'"),
        (0xDF, "OEM 8"),
        (0xE2, "\\"),
    ];
    if key == 0 {
        return "None".into();
    }
    if let Some((_, name)) = NAMES.iter().find(|(k, _)| *k == key) {
        return (*name).into();
    }
    if let Some((_, us)) = PUNCTUATION.iter().find(|(k, _)| *k == key) {
        return layout_char(key).unwrap_or_else(|| (*us).into());
    }
    match key {
        0x30..=0x39 | 0x41..=0x5A => char::from(key as u8).to_string(),
        0x60..=0x69 => format!("Num {}", key - 0x60),
        0x70..=0x87 => format!("F{}", key - 0x70 + 1),
        _ => format!("Key 0x{key:02X}"),
    }
}

/// The unshifted character a key types on the current layout; the high bit marks a dead key, which still names it.
fn layout_char(key: i32) -> Option<String> {
    #[cfg(windows)]
    {
        let typed = unsafe { MapVirtualKeyW(key as u32, 2) } & 0x7FFF;
        char::from_u32(typed)
            .filter(|c| !c.is_control() && !c.is_whitespace())
            .map(|c| c.to_uppercase().collect())
    }
    #[cfg(not(windows))]
    {
        let _ = key;
        None
    }
}

/// Common Windows virtual-key codes.
pub mod vk {
    pub const LBUTTON: i32 = 0x01;
    pub const RBUTTON: i32 = 0x02;
    pub const XBUTTON1: i32 = 0x05;
    pub const XBUTTON2: i32 = 0x06;
    pub const BACK: i32 = 0x08;
    pub const ESCAPE: i32 = 0x1B;
    pub const INSERT: i32 = 0x2D;
    pub const DELETE: i32 = 0x2E;
    pub const END: i32 = 0x23;
    pub const HOME: i32 = 0x24;
    pub const F1: i32 = 0x70;
    pub const F6: i32 = 0x75;
    pub const F10: i32 = 0x79;
    pub const LSHIFT: i32 = 0xA0;
    pub const LMENU: i32 = 0xA4;
}
