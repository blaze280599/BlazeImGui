//! A small 2D vector for layout math, convertible to and from Dear ImGui's `ImVec2`.

use std::ops::{Add, Mul, Sub};

use dear_imgui_rs::sys;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct V2 {
    pub x: f32,
    pub y: f32,
}

pub const fn v2(x: f32, y: f32) -> V2 {
    V2 { x, y }
}

impl V2 {
    pub const ZERO: V2 = v2(0.0, 0.0);

    pub fn splat(v: f32) -> V2 {
        v2(v, v)
    }

    pub fn lerp(self, other: V2, t: f32) -> V2 {
        v2(self.x + (other.x - self.x) * t, self.y + (other.y - self.y) * t)
    }
}

impl Add for V2 {
    type Output = V2;
    fn add(self, o: V2) -> V2 {
        v2(self.x + o.x, self.y + o.y)
    }
}

impl Sub for V2 {
    type Output = V2;
    fn sub(self, o: V2) -> V2 {
        v2(self.x - o.x, self.y - o.y)
    }
}

impl Mul<f32> for V2 {
    type Output = V2;
    fn mul(self, s: f32) -> V2 {
        v2(self.x * s, self.y * s)
    }
}

impl From<sys::ImVec2_c> for V2 {
    fn from(v: sys::ImVec2_c) -> V2 {
        v2(v.x, v.y)
    }
}

impl From<V2> for sys::ImVec2_c {
    fn from(v: V2) -> sys::ImVec2_c {
        sys::ImVec2_c { x: v.x, y: v.y }
    }
}

impl From<[f32; 2]> for V2 {
    fn from(v: [f32; 2]) -> V2 {
        v2(v[0], v[1])
    }
}

impl From<V2> for [f32; 2] {
    fn from(v: V2) -> [f32; 2] {
        [v.x, v.y]
    }
}
