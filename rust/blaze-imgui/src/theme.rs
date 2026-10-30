//! The active theme. [`set`] or [`toggle`] cross-fades every color over ~0.3 s; [`colors`] always holds the current
//! (possibly mid-transition) palette for custom drawing.

use std::cell::RefCell;

use dear_imgui_rs::sys;

use crate::anim;
use crate::palette::{alpha, Color, Palette};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ThemeMode {
    Dark,
    Light,
}

struct State {
    dark: Palette,
    light: Palette,
    from: Palette,
    colors: Palette,
    progress: f32,
    mode: ThemeMode,
    rounding: f32,
    changed: Option<Box<dyn FnMut(ThemeMode)>>,
}

impl State {
    fn target(&self) -> Palette {
        if self.mode == ThemeMode::Dark {
            self.dark
        } else {
            self.light
        }
    }
}

thread_local! {
    static STATE: RefCell<State> = RefCell::new(State {
        dark: Palette::default_dark(),
        light: Palette::default_light(),
        from: Palette::default_light(),
        colors: Palette::default_light(),
        progress: 1.0,
        mode: ThemeMode::Light,
        rounding: 4.0,
        changed: None,
    });
}

const TRANSITION_SECONDS: f32 = 0.32;

/// The selected mode (the target of any running transition).
pub fn mode() -> ThemeMode {
    STATE.with_borrow(|s| s.mode)
}

pub fn is_dark() -> bool {
    mode() == ThemeMode::Dark
}

/// Current colors (mid-transition while a switch animates). Read every frame; don't cache across frames.
pub fn colors() -> Palette {
    STATE.with_borrow(|s| s.colors)
}

/// Frame rounding after UI scale.
pub fn rounding() -> f32 {
    STATE.with_borrow(|s| s.rounding)
}

fn has_context() -> bool {
    unsafe { !sys::igGetCurrentContext().is_null() }
}

/// Switches theme; with `animate` every color cross-fades.
pub fn set(mode: ThemeMode, animate: bool) {
    let callback = STATE.with_borrow_mut(|s| {
        if mode == s.mode && s.progress >= 1.0 {
            return None;
        }
        s.from = s.colors;
        s.mode = mode;
        s.progress = if animate { 0.0 } else { 1.0 };
        if !animate {
            s.colors = s.target();
        }
        s.changed.take()
    });
    if !animate && has_context() {
        apply_colors(&colors());
    }
    // The callback runs outside the borrow so it may read the theme.
    if let Some(mut callback) = callback {
        callback(mode);
        STATE.with_borrow_mut(|s| {
            s.changed.get_or_insert(callback);
        });
    }
}

pub fn toggle() {
    set(if is_dark() { ThemeMode::Light } else { ThemeMode::Dark }, true);
}

/// One accent for both themes (the light theme gets a slightly deeper hover).
pub fn set_accent(accent: Color, animate: bool) {
    STATE.with_borrow_mut(|s| {
        s.from = s.colors;
        s.dark = s.dark.with_accent(accent, false);
        s.light = s.light.with_accent(accent, true);
        s.progress = if animate { 0.0 } else { 1.0 };
        if !animate {
            s.colors = s.target();
        }
    });
    if !animate && has_context() {
        apply_colors(&colors());
    }
}

/// Edits a whole palette: `theme::customize(ThemeMode::Dark, |p| p.background = hex("#0E0E10", 1.0))`.
pub fn customize(mode: ThemeMode, edit: impl FnOnce(&mut Palette)) {
    STATE.with_borrow_mut(|s| {
        s.from = s.colors;
        edit(if mode == ThemeMode::Dark { &mut s.dark } else { &mut s.light });
        s.progress = 0.0;
    });
}

/// Called after the mode changes (e.g. to save the choice).
pub fn on_changed(callback: impl FnMut(ThemeMode) + 'static) {
    STATE.with_borrow_mut(|s| s.changed = Some(Box::new(callback)));
}

/// Packs a color for draw lists, multiplying in the style alpha (so menu fades and disabled states apply).
pub fn u32(color: Color, a: f32) -> u32 {
    unsafe {
        let style_alpha = (*sys::igGetStyle()).Alpha;
        sys::igColorConvertFloat4ToU32(sys::ImVec4_c { x: color[0], y: color[1], z: color[2], w: color[3] * a * style_alpha })
    }
}

/// Writes sizes, rounding and colors into ImGui's style at a UI scale. `Overlay::setup` / `set_scale` call it.
pub fn apply_style(scale: f32) {
    unsafe {
        // From a default style each time, so applying a new scale never compounds the previous one.
        let style = &mut *sys::igGetStyle();
        let (font_base, font_main, font_dpi) = (style.FontSizeBase, style.FontScaleMain, style.FontScaleDpi);
        let defaults = sys::ImGuiStyle_ImGuiStyle();
        *style = *defaults;
        sys::ImGuiStyle_destroy(defaults);
        style.FontSizeBase = font_base;
        style.FontScaleMain = font_main;
        style.FontScaleDpi = font_dpi;

        let v = |x: f32, y: f32| sys::ImVec2_c { x, y };
        style.WindowRounding = 8.0;
        style.ChildRounding = 6.0;
        style.FrameRounding = 4.0;
        style.PopupRounding = 5.0;
        style.GrabRounding = 3.0;
        style.TabRounding = 4.0;
        style.ScrollbarRounding = 9.0;
        style.WindowBorderSize = 1.0;
        style.ChildBorderSize = 1.0;
        style.PopupBorderSize = 1.0;
        style.FrameBorderSize = 1.0;
        style.TabBorderSize = 0.0;
        style.TabBarBorderSize = 1.0;
        style.TabBarOverlineSize = 2.0;
        style.WindowPadding = v(16.0, 14.0);
        style.FramePadding = v(10.0, 8.0);
        style.ItemSpacing = v(10.0, 8.0);
        style.ItemInnerSpacing = v(8.0, 6.0);
        style.CellPadding = v(10.0, 6.0);
        style.IndentSpacing = 18.0;
        style.ScrollbarSize = 6.0;
        style.GrabMinSize = 10.0;
        style.WindowMinSize = v(160.0, 80.0);
        style.SeparatorTextBorderSize = 1.0;
        style.SeparatorTextPadding = v(0.0, 6.0);
        style.WindowTitleAlign = v(0.0, 0.5);
        style.ButtonTextAlign = v(0.5, 0.5);
        style.HoverDelayShort = 0.25;
        style.HoverDelayNormal = 0.45;
        style.AntiAliasedFill = true;
        style.AntiAliasedLines = true;
        if (scale - 1.0).abs() > 0.001 {
            sys::ImGuiStyle_ScaleAllSizes(style, scale);
        }
        let rounding = style.FrameRounding;
        STATE.with_borrow_mut(|s| s.rounding = rounding);
    }
    apply_colors(&colors());
}

/// Advances a running transition. `Overlay::render` calls it every frame.
pub fn update() {
    let colors = STATE.with_borrow_mut(|s| {
        if s.progress >= 1.0 {
            return None;
        }
        s.progress = (s.progress + anim::delta() / TRANSITION_SECONDS).min(1.0);
        s.colors = if s.progress >= 1.0 { s.target() } else { Palette::lerp(&s.from, &s.target(), anim::ease_in_out_cubic(s.progress)) };
        Some(s.colors)
    });
    if let Some(colors) = colors {
        apply_colors(&colors);
    }
}

fn apply_colors(p: &Palette) {
    let none: Color = [0.0; 4];
    let a = |c: Color, f: f32| alpha(c, c[3] * f);
    let dark = is_dark();
    let entries: [(sys::ImGuiCol_, Color); 55] = [
        (sys::ImGuiCol_Text, p.text),
        (sys::ImGuiCol_TextDisabled, p.text_muted),
        (sys::ImGuiCol_WindowBg, p.background),
        (sys::ImGuiCol_ChildBg, none),
        (sys::ImGuiCol_PopupBg, p.elevated),
        (sys::ImGuiCol_Border, p.border),
        (sys::ImGuiCol_BorderShadow, none),
        (sys::ImGuiCol_FrameBg, p.surface),
        (sys::ImGuiCol_FrameBgHovered, p.elevated),
        (sys::ImGuiCol_FrameBgActive, p.elevated),
        (sys::ImGuiCol_TitleBg, p.background),
        (sys::ImGuiCol_TitleBgActive, p.background),
        (sys::ImGuiCol_TitleBgCollapsed, p.background),
        (sys::ImGuiCol_MenuBarBg, p.sidebar),
        (sys::ImGuiCol_ScrollbarBg, none),
        (sys::ImGuiCol_ScrollbarGrab, p.field_active),
        (sys::ImGuiCol_ScrollbarGrabHovered, a(p.text_faint, 0.7)),
        (sys::ImGuiCol_ScrollbarGrabActive, p.text_faint),
        (sys::ImGuiCol_CheckMark, p.accent),
        (sys::ImGuiCol_SliderGrab, p.accent),
        (sys::ImGuiCol_SliderGrabActive, p.accent_hover),
        (sys::ImGuiCol_Button, p.field),
        (sys::ImGuiCol_ButtonHovered, p.field_hover),
        (sys::ImGuiCol_ButtonActive, p.field_active),
        (sys::ImGuiCol_Header, p.field),
        (sys::ImGuiCol_HeaderHovered, a(p.field, 0.7)),
        (sys::ImGuiCol_HeaderActive, p.field_hover),
        (sys::ImGuiCol_Separator, p.border),
        (sys::ImGuiCol_SeparatorHovered, a(p.accent, 0.6)),
        (sys::ImGuiCol_SeparatorActive, p.accent),
        (sys::ImGuiCol_ResizeGrip, none),
        (sys::ImGuiCol_ResizeGripHovered, a(p.accent, 0.35)),
        (sys::ImGuiCol_ResizeGripActive, a(p.accent, 0.6)),
        (sys::ImGuiCol_Tab, none),
        (sys::ImGuiCol_TabHovered, p.field_hover),
        (sys::ImGuiCol_TabSelected, p.field),
        (sys::ImGuiCol_TabSelectedOverline, p.accent),
        (sys::ImGuiCol_TabDimmed, none),
        (sys::ImGuiCol_TabDimmedSelected, p.field),
        (sys::ImGuiCol_TabDimmedSelectedOverline, none),
        (sys::ImGuiCol_PlotLines, p.accent),
        (sys::ImGuiCol_PlotLinesHovered, p.accent_hover),
        (sys::ImGuiCol_PlotHistogram, p.accent),
        (sys::ImGuiCol_PlotHistogramHovered, p.accent_hover),
        (sys::ImGuiCol_TableHeaderBg, p.surface),
        (sys::ImGuiCol_TableBorderStrong, p.border),
        (sys::ImGuiCol_TableBorderLight, p.border),
        (sys::ImGuiCol_TableRowBg, none),
        (sys::ImGuiCol_TableRowBgAlt, a(p.text, 0.025)),
        (sys::ImGuiCol_TextLink, p.accent),
        (sys::ImGuiCol_TextSelectedBg, a(p.accent, 0.3)),
        (sys::ImGuiCol_DragDropTarget, p.accent),
        (sys::ImGuiCol_NavCursor, a(p.accent, 0.8)),
        (sys::ImGuiCol_NavWindowingHighlight, a(p.text, 0.6)),
        (sys::ImGuiCol_ModalWindowDimBg, [0.0, 0.0, 0.0, if dark { 0.45 } else { 0.25 }]),
    ];
    unsafe {
        let style = &mut *sys::igGetStyle();
        for (index, c) in entries {
            style.Colors[index as usize] = sys::ImVec4_c { x: c[0], y: c[1], z: c[2], w: c[3] };
        }
        style.Colors[sys::ImGuiCol_NavWindowingDimBg as usize] = sys::ImVec4_c { x: 0.0, y: 0.0, z: 0.0, w: 0.3 };
    }
}
