//! The semantic colors of one theme. Widgets read only these tokens; read the live values from
//! [`crate::theme::colors`].

/// An RGBA color, each channel 0..1.
pub type Color = [f32; 4];

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Palette {
    /// Menu window.
    pub background: Color,
    /// Secondary panels.
    pub sidebar: Color,
    /// Cards, value boxes and fields on the background.
    pub surface: Color,
    /// Popups, dropdown lists, tooltips, toasts.
    pub elevated: Color,
    /// Tracks and pressed states, at rest / hovered / pressed.
    pub field: Color,
    pub field_hover: Color,
    pub field_active: Color,
    /// A control on a field.
    pub raised: Color,
    /// Hairlines.
    pub border: Color,
    pub text: Color,
    pub text_muted: Color,
    pub text_faint: Color,
    /// Selection and fills (an ink: near-black in light, near-white in dark), its hover, and text on it.
    pub accent: Color,
    pub accent_hover: Color,
    pub on_accent: Color,
    /// Slider grips.
    pub knob: Color,
    /// The brand mark's dot; the rest of the menu stays neutral.
    pub brand: Color,
    pub success: Color,
    pub warning: Color,
    pub danger: Color,
}

/// "#RRGGBB" or "#RRGGBBAA" to a color; `alpha` applies when the string has no alpha.
pub fn hex(hex: &str, alpha: f32) -> Color {
    let hex = hex.trim_start_matches('#');
    let channel = |i: usize| hex.get(i..i + 2).and_then(|pair| u8::from_str_radix(pair, 16).ok()).map_or(0.0, |v| v as f32 / 255.0);
    let a = if hex.len() >= 8 { channel(6) } else { alpha };
    [channel(0), channel(2), channel(4), a]
}

/// Lightens (positive) or darkens (negative) a color by mixing toward white or black.
pub fn shade(c: Color, amount: f32) -> Color {
    let target = if amount >= 0.0 { 1.0 } else { 0.0 };
    let t = amount.abs();
    [c[0] + (target - c[0]) * t, c[1] + (target - c[1]) * t, c[2] + (target - c[2]) * t, c[3]]
}

/// The same color with a new alpha.
pub fn alpha(c: Color, a: f32) -> Color {
    [c[0], c[1], c[2], a]
}

/// Linear blend between two colors.
pub fn lerp(a: Color, b: Color, t: f32) -> Color {
    [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, a[3] + (b[3] - a[3]) * t]
}

impl Palette {
    /// This palette with another accent (and its derived hover shade).
    pub fn with_accent(&self, accent: Color, light_theme: bool) -> Palette {
        Palette { accent, accent_hover: shade(accent, if light_theme { -0.08 } else { 0.08 }), ..*self }
    }

    pub fn lerp(a: &Palette, b: &Palette, t: f32) -> Palette {
        let l = |x: Color, y: Color| lerp(x, y, t);
        Palette {
            background: l(a.background, b.background),
            sidebar: l(a.sidebar, b.sidebar),
            surface: l(a.surface, b.surface),
            elevated: l(a.elevated, b.elevated),
            field: l(a.field, b.field),
            field_hover: l(a.field_hover, b.field_hover),
            field_active: l(a.field_active, b.field_active),
            raised: l(a.raised, b.raised),
            border: l(a.border, b.border),
            text: l(a.text, b.text),
            text_muted: l(a.text_muted, b.text_muted),
            text_faint: l(a.text_faint, b.text_faint),
            accent: l(a.accent, b.accent),
            accent_hover: l(a.accent_hover, b.accent_hover),
            on_accent: l(a.on_accent, b.on_accent),
            knob: l(a.knob, b.knob),
            brand: l(a.brand, b.brand),
            success: l(a.success, b.success),
            warning: l(a.warning, b.warning),
            danger: l(a.danger, b.danger),
        }
    }

    pub fn default_dark() -> Palette {
        Palette {
            background: hex("#17171B", 1.0),
            sidebar: hex("#17171B", 1.0),
            surface: hex("#1D1D22", 1.0),
            elevated: hex("#202026", 1.0),
            field: hex("#2A2A31", 1.0),
            field_hover: hex("#313139", 1.0),
            field_active: hex("#393941", 1.0),
            raised: hex("#33333B", 1.0),
            border: hex("#FFFFFF", 0.08),
            text: hex("#E7E7EB", 1.0),
            text_muted: hex("#8C8C96", 1.0),
            text_faint: hex("#56565F", 1.0),
            accent: hex("#E7E7EB", 1.0),
            accent_hover: hex("#FFFFFF", 1.0),
            on_accent: hex("#17171B", 1.0),
            knob: hex("#F2F2F5", 1.0),
            brand: hex("#FF5C9D", 1.0),
            success: hex("#3DD68C", 1.0),
            warning: hex("#F5A524", 1.0),
            danger: hex("#FF6B6B", 1.0),
        }
    }

    pub fn default_light() -> Palette {
        Palette {
            background: hex("#ECECF0", 1.0),
            sidebar: hex("#ECECF0", 1.0),
            surface: hex("#F3F3F6", 1.0),
            elevated: hex("#F7F7F9", 1.0),
            field: hex("#E1E1E6", 1.0),
            field_hover: hex("#D9D9DF", 1.0),
            field_active: hex("#CFCFD6", 1.0),
            raised: hex("#F9F9FB", 1.0),
            border: hex("#0B0B12", 0.10),
            text: hex("#2B2B30", 1.0),
            text_muted: hex("#8E8E98", 1.0),
            text_faint: hex("#B6B6BF", 1.0),
            accent: hex("#2B2B30", 1.0),
            accent_hover: hex("#3B3B41", 1.0),
            on_accent: hex("#F3F3F6", 1.0),
            knob: hex("#F9F9FB", 1.0),
            brand: hex("#FF5C9D", 1.0),
            success: hex("#2EA44F", 1.0),
            warning: hex("#E8820C", 1.0),
            danger: hex("#E5484D", 1.0),
        }
    }
}
