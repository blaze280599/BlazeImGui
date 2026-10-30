//! Global key state through `GetAsyncKeyState` (Windows virtual-key codes). Elsewhere every key reads as up.

use std::cell::Cell;

thread_local! {
    static KEYS: Cell<([bool; 256], [bool; 256])> = const { Cell::new(([false; 256], [false; 256])) };
}

#[cfg(windows)]
#[link(name = "user32")]
unsafe extern "system" {
    fn GetAsyncKeyState(key: i32) -> i16;
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

/// "Insert", "F5", "Mouse 4", "None".
pub fn key_name(key: i32) -> String {
    const NAMES: &[(i32, &str)] = &[
        (0x01, "Mouse 1"),
        (0x02, "Mouse 2"),
        (0x04, "Mouse 3"),
        (0x05, "Mouse 4"),
        (0x06, "Mouse 5"),
        (0x08, "Back"),
        (0x09, "Tab"),
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
        (0x2D, "Insert"),
        (0x2E, "Delete"),
        (0xA0, "L Shift"),
        (0xA1, "R Shift"),
        (0xA2, "L Ctrl"),
        (0xA3, "R Ctrl"),
        (0xA4, "L Alt"),
        (0xA5, "R Alt"),
    ];
    if key == 0 {
        return "None".into();
    }
    if let Some((_, name)) = NAMES.iter().find(|(k, _)| *k == key) {
        return (*name).into();
    }
    match key {
        0x30..=0x39 | 0x41..=0x5A => char::from(key as u8).to_string(),
        0x60..=0x69 => format!("Num {}", key - 0x60),
        0x70..=0x87 => format!("F{}", key - 0x70 + 1),
        _ => format!("Key 0x{key:02X}"),
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
