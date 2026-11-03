//! Embedded Inter (Regular, Medium, SemiBold, Bold) with the Lucide icons merged into every weight, so icons mix into any
//! string: `format!("{}  Hello", icons::INFO)`. ImGui 1.92 fonts have no fixed size, so a [`Font`] is a weight plus
//! the size it is used at.

use std::cell::Cell;

use dear_imgui_rs::sys;

use crate::icons;
use crate::math::{v2, V2};

static INTER_REGULAR: &[u8] = include_bytes!("../fonts/Inter-Regular.ttf");
static INTER_MEDIUM: &[u8] = include_bytes!("../fonts/Inter-Medium.ttf");
static INTER_SEMIBOLD: &[u8] = include_bytes!("../fonts/Inter-SemiBold.ttf");
static INTER_BOLD: &[u8] = include_bytes!("../fonts/Inter-Bold.ttf");
static LUCIDE: &[u8] = include_bytes!("../fonts/lucide.ttf");

// The icons own their codepoints: a text font must never answer for them (sources are searched in order).
static ICON_RANGE: [sys::ImWchar; 3] = [icons::RANGE_START as sys::ImWchar, icons::RANGE_END as sys::ImWchar, 0];

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FontWeight {
    Regular,
    Medium,
    SemiBold,
    Bold,
}

/// An ImGui 1.92 font plus the size (in unscaled pixels) it is used at.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Font {
    pub font: *mut sys::ImFont,
    pub size: f32,
}

impl Font {
    const NONE: Font = Font { font: std::ptr::null_mut(), size: 0.0 };

    /// Its size in pixels after the style's font scale, for draw-list text.
    pub fn px(&self) -> f32 {
        unsafe {
            let style = &*sys::igGetStyle();
            self.size * style.FontScaleMain * style.FontScaleDpi
        }
    }

    pub fn calc_text_size(&self, text: &str) -> V2 {
        self.calc_text_size_wrapped(text, 0.0)
    }

    /// The size of `text` wrapped at `wrap_width` pixels (0: no wrapping).
    pub fn calc_text_size_wrapped(&self, text: &str, wrap_width: f32) -> V2 {
        if self.font.is_null() {
            return V2::ZERO;
        }
        let range = text.as_bytes().as_ptr_range();
        unsafe {
            sys::ImFont_CalcTextSizeA(
                self.font,
                self.px(),
                f32::MAX,
                wrap_width,
                range.start.cast(),
                range.end.cast(),
                std::ptr::null_mut(),
            )
            .into()
        }
    }
}

#[derive(Clone, Copy)]
struct Set {
    weights: [*mut sys::ImFont; 4],
    body: f32,
}

thread_local! {
    static SET: Cell<Set> = const { Cell::new(Set { weights: [std::ptr::null_mut(); 4], body: 15.0 }) };
}

fn weight(w: FontWeight) -> *mut sys::ImFont {
    SET.get().weights[w as usize]
}

fn body() -> f32 {
    SET.get().body
}

/// Body text (the ImGui default font).
pub fn regular() -> Font {
    Font { font: weight(FontWeight::Regular), size: body() }
}
/// Body size, a little emphasis.
pub fn medium() -> Font {
    Font { font: weight(FontWeight::Medium), size: body() }
}
/// Body size, semibold: card titles.
pub fn semibold() -> Font {
    Font { font: weight(FontWeight::SemiBold), size: body() }
}
/// Body - 2 px: captions, hints, badges.
pub fn small() -> Font {
    Font { font: weight(FontWeight::Regular), size: body() - 2.0 }
}
/// Small semibold: version labels, table headers.
pub fn small_semibold() -> Font {
    Font { font: weight(FontWeight::SemiBold), size: body() - 2.0 }
}
/// Stat values, big numbers.
pub fn heading() -> Font {
    Font { font: weight(FontWeight::SemiBold), size: (body() * 1.34).round() }
}
/// Menu column titles (page, tab, status).
pub fn title() -> Font {
    Font { font: weight(FontWeight::Medium), size: (body() * 1.27).round() }
}
/// The rail's brand mark.
pub fn brand() -> Font {
    Font { font: weight(FontWeight::SemiBold), size: (body() * 1.87).round() }
}
/// Large icons (rail).
pub fn icon_large() -> Font {
    Font { font: weight(FontWeight::Regular), size: (body() * 1.4).round() }
}

/// The body font size in pixels before scale.
pub fn body_size() -> f32 {
    body()
}

/// Another weight at a size (unscaled px). Weights are shared fonts, so this is cheap at any time after [`load`].
pub fn add(weight_: FontWeight, size: f32) -> Font {
    Font { font: weight(weight_), size }
}

pub fn push(font: Font) {
    unsafe { sys::igPushFont(font.font, font.size) }
}

pub fn pop() {
    unsafe { sys::igPopFont() }
}

fn add_weight(data: &'static [u8], px: f32, name: &str) -> *mut sys::ImFont {
    unsafe {
        let atlas = (*sys::igGetIO_Nil()).Fonts;

        let text = sys::ImFontConfig_ImFontConfig();
        (*text).FontDataOwnedByAtlas = false; // the data is static
        (*text).OversampleH = 2;
        (*text).OversampleV = 1;
        (*text).PixelSnapH = true;
        (*text).GlyphExcludeRanges = ICON_RANGE.as_ptr();
        write_name(&mut (*text).Name, name);
        let font = sys::ImFontAtlas_AddFontFromMemoryTTF(atlas, data.as_ptr() as *mut _, data.len() as i32, px, text, std::ptr::null());
        sys::ImFontConfig_destroy(text);

        // Icons are square: a fixed advance keeps icon columns aligned, and a small downward offset centers the glyph
        // on the text's x-height instead of the baseline.
        let icons = sys::ImFontConfig_ImFontConfig();
        (*icons).MergeMode = true;
        (*icons).FontDataOwnedByAtlas = false;
        (*icons).OversampleH = 1;
        (*icons).OversampleV = 1;
        (*icons).PixelSnapH = true;
        (*icons).GlyphMinAdvanceX = px;
        (*icons).GlyphOffset = v2(0.0, (px * 0.14).round()).into();
        write_name(&mut (*icons).Name, &format!("Lucide ({name})"));
        sys::ImFontAtlas_AddFontFromMemoryTTF(atlas, LUCIDE.as_ptr() as *mut _, LUCIDE.len() as i32, px, icons, ICON_RANGE.as_ptr());
        sys::ImFontConfig_destroy(icons);
        font
    }
}

fn write_name(target: &mut [std::os::raw::c_char; 40], name: &str) {
    for (slot, byte) in target.iter_mut().zip(name.bytes().take(39).chain(std::iter::once(0))) {
        *slot = byte as std::os::raw::c_char;
    }
}

/// Adds the fonts to the atlas and makes Regular the default. `Overlay::setup` calls it; call it yourself only when
/// you drive ImGui without the overlay. Needs a current ImGui context and must run before the first frame.
pub fn load(body_size: f32) {
    let body = body_size.round();
    let weights = [
        add_weight(INTER_REGULAR, body, "Inter Regular"),
        add_weight(INTER_MEDIUM, body, "Inter Medium"),
        add_weight(INTER_SEMIBOLD, body, "Inter SemiBold"),
        add_weight(INTER_BOLD, body, "Inter Bold"),
    ];
    SET.set(Set { weights, body });
    unsafe {
        (*sys::igGetIO_Nil()).FontDefault = weights[0];
        (*sys::igGetStyle()).FontSizeBase = body;
    }
}

impl Default for Font {
    fn default() -> Self {
        Font::NONE
    }
}
