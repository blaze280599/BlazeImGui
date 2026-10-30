//! Frame-rate independent easing. Values are keyed by ImGui IDs and live for the whole session.

use std::cell::RefCell;
use std::collections::HashMap;

use dear_imgui_rs::sys;

use crate::math::V2;
use crate::palette::Color;

thread_local! {
    static VALUES: RefCell<HashMap<u32, f32>> = RefCell::new(HashMap::new());
}

/// Seconds since the last frame, clamped to 0.1 so a stall never makes animations jump.
pub fn delta() -> f32 {
    unsafe { (*sys::igGetIO_Nil()).DeltaTime.clamp(0.0, 0.1) }
}

/// Eases the value stored for `id` toward `target`. `speed` ~ 1 / time constant (10 relaxed, 16 snappy).
/// The first call starts at `initial` (or at the target when `None`).
pub fn to(id: u32, target: f32, speed: f32, initial: Option<f32>) -> f32 {
    let step = 1.0 - (-speed * delta()).exp();
    VALUES.with_borrow_mut(|values| {
        let value = values.entry(id).or_insert(initial.unwrap_or(target));
        *value += (target - *value) * step;
        if (target - *value).abs() < 0.0005 {
            *value = target;
        }
        *value
    })
}

/// Toward 1 while `on`, toward 0 otherwise.
pub fn toggle(id: u32, on: bool, speed: f32) -> f32 {
    to(id, if on { 1.0 } else { 0.0 }, speed, None)
}

pub fn set(id: u32, value: f32) {
    VALUES.with_borrow_mut(|values| {
        values.insert(id, value);
    });
}

pub fn lerp(a: f32, b: f32, t: f32) -> f32 {
    a + (b - a) * t
}

pub fn lerp_v2(a: V2, b: V2, t: f32) -> V2 {
    a.lerp(b, t)
}

pub fn lerp_color(a: Color, b: Color, t: f32) -> Color {
    crate::palette::lerp(a, b, t)
}

pub fn ease_out_cubic(t: f32) -> f32 {
    1.0 - (1.0 - t.clamp(0.0, 1.0)).powi(3)
}

pub fn ease_in_out_cubic(t: f32) -> f32 {
    let t = t.clamp(0.0, 1.0);
    if t < 0.5 {
        4.0 * t * t * t
    } else {
        1.0 - (-2.0 * t + 2.0).powi(3) / 2.0
    }
}
