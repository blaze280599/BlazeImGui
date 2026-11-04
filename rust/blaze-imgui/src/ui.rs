//! Themed, animated widgets. Labeled controls stack: the label on its own line, the control under it at
//! [`control_width`]. A description shows under ABOUT in the menu's status column while the control is hovered (under
//! the control when the menu has no status column). Text after "##" is hidden but keeps the ID unique. Controls return
//! true on the frame the value changes. Every function takes the frame's [`Ui`] as proof that a frame is open; plain
//! `dear-imgui-rs` widgets are themed too and mix freely.

use std::cell::{Cell, RefCell};
use std::ffi::{CStr, CString};

use dear_imgui_rs::{sys, Ui};

use crate::anim;
use crate::fonts::{self, Font};
use crate::icons;
use crate::input;
use crate::math::{v2, V2};
use crate::palette::{self, Color, Palette};
use crate::theme;

/// Semantic color for badges, toasts, readouts, progress bars.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum Tone {
    #[default]
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
}

/// Visual weight of [`button`].
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum ButtonKind {
    /// Outlined.
    #[default]
    Secondary,
    /// Filled with the accent ink.
    Primary,
    Ghost,
    Danger,
}

thread_local! {
    static SCALE: Cell<f32> = const { Cell::new(1.0) };
    static CURRENT_ABOUT: RefCell<String> = const { RefCell::new(String::new()) };
    static CURRENT_ABOUT_TITLE: RefCell<String> = const { RefCell::new(String::new()) };
    static INLINE_DESCRIPTIONS: Cell<bool> = const { Cell::new(false) };
    static READOUT_SPLIT: Cell<f32> = const { Cell::new(0.42) };
    static CAPTURING_KEY: Cell<u32> = const { Cell::new(0) };
}

/// UI scale (sizes; fonts follow through `style.FontScaleMain`). Set it with `Overlay::set_scale`.
pub fn scale() -> f32 {
    SCALE.get()
}

pub(crate) fn set_scale(scale: f32) {
    SCALE.set(scale);
}

/// The text under ABOUT: the last hovered control's description, or the tab's.
pub fn current_about() -> String {
    CURRENT_ABOUT.with_borrow(|s| s.clone())
}

pub fn set_current_about(text: &str) {
    CURRENT_ABOUT.with_borrow_mut(|s| {
        s.clear();
        s.push_str(text);
    });
}

/// The name of the control [`current_about`] describes, shown above it; empty for the tab's description.
pub fn current_about_title() -> String {
    CURRENT_ABOUT_TITLE.with_borrow(|s| s.clone())
}

pub fn set_current_about_title(title: &str) {
    CURRENT_ABOUT_TITLE.with_borrow_mut(|s| {
        s.clear();
        s.push_str(title);
    });
}

/// Descriptions under their controls instead of under ABOUT (the menu sets it each frame).
pub fn set_inline_descriptions(inline: bool) {
    INLINE_DESCRIPTIONS.set(inline);
}

/// Label column of [`readout`], as a fraction of the row (at least 110 px). Default 0.42.
pub fn set_readout_split(split: f32) {
    READOUT_SPLIT.set(split);
}

/// True while a [`keybind`] waits for a key (hosts should ignore their hotkeys meanwhile).
pub fn is_capturing_key() -> bool {
    CAPTURING_KEY.get() != 0
}

// ---------------------------------------------------------------------------------------------------------- helpers

pub(crate) fn s(value: f32) -> f32 {
    value * SCALE.get()
}

pub(crate) fn s2(x: f32, y: f32) -> V2 {
    v2(x, y) * SCALE.get()
}

pub(crate) fn p() -> Palette {
    theme::colors()
}

pub(crate) fn col(color: Color, alpha: f32) -> u32 {
    theme::u32(color, alpha)
}

pub(crate) fn cstring(text: &str) -> CString {
    CString::new(text.replace('\0', "")).unwrap_or_default()
}

fn text_range(text: &str) -> (*const std::os::raw::c_char, *const std::os::raw::c_char) {
    let range = text.as_bytes().as_ptr_range();
    (range.start.cast(), range.end.cast())
}

pub(crate) fn calc_text_size(text: &str) -> V2 {
    let (begin, end) = text_range(text);
    unsafe { sys::igCalcTextSize(begin, end, false, -1.0).into() }
}

pub(crate) fn text_unformatted(text: &str) {
    let (begin, end) = text_range(text);
    unsafe { sys::igTextUnformatted(begin, end) }
}

pub(crate) fn push_id(label: &str) {
    let (begin, end) = text_range(label);
    unsafe { sys::igPushID_StrStr(begin, end) }
}

pub(crate) fn push_id_int(id: i32) {
    unsafe { sys::igPushID_Int(id) }
}

pub(crate) fn pop_id() {
    unsafe { sys::igPopID() }
}

pub(crate) fn get_id(key: &CStr) -> u32 {
    unsafe { sys::igGetID_Str(key.as_ptr()) }
}

pub(crate) fn get_id_str(key: &str) -> u32 {
    let (begin, end) = text_range(key);
    unsafe { sys::igGetID_StrStr(begin, end) }
}

pub(crate) fn cursor_screen_pos() -> V2 {
    unsafe { sys::igGetCursorScreenPos().into() }
}

pub(crate) fn set_cursor_screen_pos(pos: V2) {
    unsafe { sys::igSetCursorScreenPos(pos.into()) }
}

pub(crate) fn set_cursor_pos(pos: V2) {
    unsafe { sys::igSetCursorPos(pos.into()) }
}

pub(crate) fn cursor_pos_y() -> f32 {
    unsafe { sys::igGetCursorPosY() }
}

pub(crate) fn set_cursor_pos_y(y: f32) {
    unsafe { sys::igSetCursorPosY(y) }
}

pub(crate) fn content_region_avail() -> V2 {
    unsafe { sys::igGetContentRegionAvail().into() }
}

pub(crate) fn frame_height() -> f32 {
    unsafe { sys::igGetFrameHeight() }
}

pub(crate) fn text_line_height() -> f32 {
    unsafe { sys::igGetTextLineHeight() }
}

pub(crate) fn dummy(size: V2) {
    unsafe { sys::igDummy(size.into()) }
}

pub(crate) fn same_line(offset: f32, spacing: f32) {
    unsafe { sys::igSameLine(offset, spacing) }
}

pub(crate) fn invisible_button(id: &str, size: V2) -> bool {
    let id = cstring(id);
    unsafe { sys::igInvisibleButton(id.as_ptr(), v2(size.x.max(1.0), size.y.max(1.0)).into(), 0) }
}

pub(crate) fn is_item_hovered() -> bool {
    unsafe { sys::igIsItemHovered(0) }
}

pub(crate) fn is_item_active() -> bool {
    unsafe { sys::igIsItemActive() }
}

pub(crate) fn item_id() -> u32 {
    unsafe { sys::igGetItemID() }
}

pub(crate) fn hand_cursor() {
    unsafe { sys::igSetMouseCursor(sys::ImGuiMouseCursor_Hand as _) }
}

pub(crate) fn style() -> &'static sys::ImGuiStyle {
    unsafe { &*sys::igGetStyle() }
}

pub(crate) fn begin_group() {
    unsafe { sys::igBeginGroup() }
}

pub(crate) fn end_group() {
    unsafe { sys::igEndGroup() }
}

pub(crate) fn push_style_color(index: sys::ImGuiCol_, c: Color) {
    unsafe { sys::igPushStyleColor_Vec4(index as _, sys::ImVec4_c { x: c[0], y: c[1], z: c[2], w: c[3] }) }
}

pub(crate) fn pop_style_color(count: i32) {
    unsafe { sys::igPopStyleColor(count) }
}

pub(crate) fn push_style_var(index: sys::ImGuiStyleVar_, value: f32) {
    unsafe { sys::igPushStyleVar_Float(index as _, value) }
}

pub(crate) fn push_style_var_v2(index: sys::ImGuiStyleVar_, value: V2) {
    unsafe { sys::igPushStyleVar_Vec2(index as _, value.into()) }
}

pub(crate) fn pop_style_var(count: i32) {
    unsafe { sys::igPopStyleVar(count) }
}

// Dear ImGui 1.93 aligns strokes differently; the legacy alignment keeps hairlines one pixel wide, as in the C# and C++
// versions (which run on 1.91 / 1.92).
const LEGACY_STROKE: sys::ImDrawFlags = sys::ImDrawFlags_StrokeLegacy as sys::ImDrawFlags;

/// A thin wrapper over an `ImDrawList` for the drawing calls the widgets use.
#[derive(Clone, Copy)]
pub struct Draw(pub *mut sys::ImDrawList);

impl Draw {
    /// The current window's draw list.
    pub fn window() -> Draw {
        Draw(unsafe { sys::igGetWindowDrawList() })
    }

    /// The draw list behind every window of the main viewport.
    pub fn background() -> Draw {
        Draw(unsafe { sys::igGetBackgroundDrawList(sys::igGetMainViewport()) })
    }

    pub fn rect_filled(self, min: V2, max: V2, color: u32, rounding: f32) {
        unsafe { sys::ImDrawList_AddRectFilled(self.0, min.into(), max.into(), color, rounding, 0) }
    }

    pub fn rect(self, min: V2, max: V2, color: u32, rounding: f32) {
        unsafe { sys::ImDrawList_AddRect(self.0, min.into(), max.into(), color, rounding, 1.0, LEGACY_STROKE) }
    }

    pub fn line(self, a: V2, b: V2, color: u32, thickness: f32) {
        unsafe { sys::ImDrawList_AddLine(self.0, a.into(), b.into(), color, thickness, LEGACY_STROKE) }
    }

    pub fn circle_filled(self, center: V2, radius: f32, color: u32) {
        unsafe { sys::ImDrawList_AddCircleFilled(self.0, center.into(), radius, color, 0) }
    }

    pub fn circle(self, center: V2, radius: f32, color: u32, thickness: f32) {
        unsafe { sys::ImDrawList_AddCircle(self.0, center.into(), radius, color, 32, thickness, 0) }
    }

    /// Text in the current font.
    pub fn text(self, pos: V2, color: u32, text: &str) {
        let (begin, end) = text_range(text);
        unsafe { sys::ImDrawList_AddText_Vec2(self.0, pos.into(), color, begin, end) }
    }

    pub fn text_font(self, font: Font, pos: V2, color: u32, text: &str) {
        let (begin, end) = text_range(text);
        unsafe { sys::ImDrawList_AddText_FontPtr(self.0, font.font, font.px(), pos.into(), color, begin, end, 0.0, std::ptr::null()) }
    }

    pub fn push_clip_rect(self, min: V2, max: V2) {
        unsafe { sys::ImDrawList_PushClipRect(self.0, min.into(), max.into(), true) }
    }

    pub fn pop_clip_rect(self) {
        unsafe { sys::ImDrawList_PopClipRect(self.0) }
    }

    pub fn channels_split(self, count: i32) {
        unsafe { sys::ImDrawList_ChannelsSplit(self.0, count) }
    }

    pub fn channels_set_current(self, index: i32) {
        unsafe { sys::ImDrawList_ChannelsSetCurrent(self.0, index) }
    }

    pub fn channels_merge(self) {
        unsafe { sys::ImDrawList_ChannelsMerge(self.0) }
    }
}

/// The visible part of a label (before "##").
pub fn visible(label: &str) -> &str {
    label.find("##").map_or(label, |end| &label[..end])
}

/// The color for a [`Tone`] in the current theme.
pub fn tone_color(tone: Tone) -> Color {
    let p = p();
    match tone {
        Tone::Accent => p.accent,
        Tone::Success => p.success,
        Tone::Warning => p.warning,
        Tone::Danger => p.danger,
        Tone::Neutral => p.text_muted,
    }
}

// A control's label on its own line, a little closer to the control than to the row above.
fn label_line(label: &str) {
    let text = visible(label);
    if text.is_empty() {
        return;
    }
    text_unformatted(text);
    set_cursor_pos_y(cursor_pos_y() - s(1.0));
}

// Extra room after a labeled control, so rows read as separate groups.
fn gap() {
    dummy(v2(0.0, s(8.0)));
}

// ---------------------------------------------------------------------------------------------------------- layout

/// Colored text, safe for any string (no printf formatting).
pub fn text(_ui: &Ui, text: &str, color: Color) {
    push_style_color(sys::ImGuiCol_Text, color);
    text_unformatted(text);
    pop_style_color(1);
}

/// Text in a [`Tone`] color.
pub fn text_tone(ui: &Ui, value: &str, tone: Tone) {
    text(ui, value, tone_color(tone));
}

/// Text wrapped at the right edge of the content region.
pub fn text_wrapped(_ui: &Ui, text: &str) {
    unsafe { sys::igPushTextWrapPos(0.0) };
    text_unformatted(text);
    unsafe { sys::igPopTextWrapPos() };
}

/// Muted wrapped text.
pub fn hint(ui: &Ui, text: &str) {
    push_style_color(sys::ImGuiCol_Text, p().text_muted);
    text_wrapped(ui, text);
    pop_style_color(1);
}

/// A quiet uppercase label ("AIM ASSIST", "ABOUT") heading the rows below it.
pub fn caption(ui: &Ui, value: &str) {
    fonts::push(fonts::small());
    text(ui, &visible(value).to_uppercase(), p().text_faint);
    fonts::pop();
    dummy(v2(0.0, s(4.0)));
}

/// A caption that groups the rows below it, with room above when it follows other content.
pub fn section(ui: &Ui, title: &str) {
    if cursor_pos_y() > style().WindowPadding.y + 1.0 {
        dummy(v2(0.0, s(6.0)));
    }
    caption(ui, title);
}

/// A status line: a muted label and its value in a column to the right; `tone` colors the value (Neutral: text).
pub fn readout(ui: &Ui, label: &str, value: &str, tone: Tone) {
    let x0 = unsafe { sys::igGetCursorPosX() };
    let split = s(110.0).max(content_region_avail().x * READOUT_SPLIT.get());
    // A little more air than plain text lines (added above, so nothing extends past the last row).
    set_cursor_pos_y(cursor_pos_y() + s(3.0));
    text(ui, label, p().text_muted);
    same_line(x0 + split, -1.0);
    text(ui, value, if tone == Tone::Neutral { p().text } else { tone_color(tone) });
}

/// Attaches a description to the previous item or group: shown under ABOUT while hovered, or right under it when the
/// menu has no status column.
pub fn about(ui: &Ui, description: Option<&str>) {
    about_core(ui, "", description);
}

/// [`about`] with the control's name shown above the description under ABOUT (text after "##" is dropped).
pub fn about_with_title(ui: &Ui, title: &str, description: Option<&str>) {
    about_core(ui, title, description);
}

fn about_core(ui: &Ui, title: &str, description: Option<&str>) {
    let Some(description) = description else { return };
    if unsafe { sys::igIsItemHovered(sys::ImGuiHoveredFlags_AllowWhenBlockedByActiveItem as _) } {
        set_current_about(description);
        set_current_about_title(visible(title));
    }
    if !INLINE_DESCRIPTIONS.get() {
        return;
    }
    fonts::push(fonts::small());
    push_style_color(sys::ImGuiCol_Text, p().text_muted);
    set_cursor_pos_y(cursor_pos_y() - s(3.0));
    text_wrapped(ui, description);
    pop_style_color(1);
    fonts::pop();
}

/// A rounded card that grows to fit its content. Always pair with [`end_card`].
pub fn begin_card(ui: &Ui, id: &str, title: Option<&str>, icon: Option<&str>, width: f32) {
    push_style_color(sys::ImGuiCol_ChildBg, p().surface);
    push_style_color(sys::ImGuiCol_Border, p().border);
    push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, s2(14.0, 12.0));
    push_style_var(sys::ImGuiStyleVar_ChildRounding, s(6.0));
    let id = cstring(id);
    unsafe {
        sys::igBeginChild_Str(
            id.as_ptr(),
            v2(width, 0.0).into(),
            (sys::ImGuiChildFlags_Borders | sys::ImGuiChildFlags_AutoResizeY | sys::ImGuiChildFlags_AlwaysUseWindowPadding) as _,
            (sys::ImGuiWindowFlags_NoScrollbar | sys::ImGuiWindowFlags_NoScrollWithMouse) as _,
        );
    }
    pop_style_var(2);
    pop_style_color(2);
    let Some(title) = title else { return };
    fonts::push(fonts::medium());
    if let Some(icon) = icon {
        text(ui, icon, p().text_muted);
        same_line(0.0, s(8.0));
    }
    text_unformatted(title);
    fonts::pop();
}

pub fn end_card(_ui: &Ui) {
    unsafe { sys::igEndChild() }
}

/// Equal columns: [`next_cell`] before each cell, [`end_grid`] only when this returned true.
pub fn begin_grid(_ui: &Ui, id: &str, columns: i32) -> bool {
    push_style_var_v2(sys::ImGuiStyleVar_CellPadding, s2(6.0, 6.0));
    let id = cstring(id);
    let open = unsafe {
        sys::igBeginTable(
            id.as_ptr(),
            columns,
            (sys::ImGuiTableFlags_SizingStretchSame | sys::ImGuiTableFlags_NoPadOuterX) as _,
            v2(0.0, 0.0).into(),
            0.0,
        )
    };
    pop_style_var(1);
    open
}

pub fn next_cell(_ui: &Ui) {
    unsafe { sys::igTableNextColumn() };
}

pub fn end_grid(_ui: &Ui) {
    unsafe { sys::igEndTable() }
}

/// A hairline divider with breathing room.
pub fn divider(_ui: &Ui) {
    dummy(v2(0.0, s(2.0)));
    let start = cursor_screen_pos();
    let width = content_region_avail().x;
    Draw::window().line(start, start + v2(width, 0.0), col(p().border, 1.0), 1.0);
    dummy(v2(width, s(6.0)));
}

/// Same line, with the cursor moved so an item `width` wide ends at the right edge.
pub fn same_line_right(_ui: &Ui, width: f32) {
    same_line(0.0, -1.0);
    unsafe { sys::igSetCursorPosX(sys::igGetCursorPosX() + (content_region_avail().x - width).max(0.0)) };
}

/// The size [`badge`] takes for this text.
pub fn badge_size(_ui: &Ui, text: &str) -> V2 {
    fonts::push(fonts::small());
    let size = calc_text_size(text) + s2(7.0, 2.0) * 2.0;
    fonts::pop();
    size
}

/// A small rounded pill, inline with text.
pub fn badge(_ui: &Ui, text: &str, tone: Tone) {
    fonts::push(fonts::small());
    let size = calc_text_size(text);
    let padding = s2(7.0, 2.0);
    let bx = size + padding * 2.0;
    let mut start = cursor_screen_pos();
    let line_height = unsafe { sys::igGetTextLineHeightWithSpacing() };
    // Centered on the item it follows when it shares its top edge (after SameLine), else on a frame-height line.
    let previous_min: V2 = unsafe { sys::igGetItemRectMin().into() };
    let previous_max: V2 = unsafe { sys::igGetItemRectMax().into() };
    if (previous_min.y - start.y).abs() < 1.0 && previous_max.y > previous_min.y {
        start.y = (previous_min.y + previous_max.y - bx.y) * 0.5;
    } else {
        start.y += ((frame_height() - bx.y) * 0.5).max(0.0);
    }
    let color = if tone == Tone::Neutral { p().text_muted } else { tone_color(tone) };
    let draw = Draw::window();
    draw.rect_filled(start, start + bx, col(color, if theme::is_dark() { 0.16 } else { 0.12 }), bx.y * 0.5);
    draw.text(start + padding, col(color, 1.0), text);
    dummy(v2(bx.x, bx.y.min(line_height)));
    fonts::pop();
}

/// KPI tile: muted label, big value, optional colored change ("+12%").
pub fn stat(_ui: &Ui, label: &str, value: &str, change: Option<&str>, change_tone: Tone) {
    let width = content_region_avail().x;
    let height = s(78.0);
    let start = cursor_screen_pos();
    let draw = Draw::window();
    draw.rect_filled(start, start + v2(width, height), col(p().surface, 1.0), s(6.0));
    draw.rect(start, start + v2(width, height), col(p().border, 1.0), s(6.0));
    let pad = s2(14.0, 12.0);
    draw.text_font(fonts::small(), start + pad, col(p().text_muted, 1.0), label);
    let value_pos = start + pad + v2(0.0, fonts::small().px() + s(8.0));
    draw.text_font(fonts::heading(), value_pos, col(p().text, 1.0), value);
    if let Some(change) = change {
        let value_width = fonts::heading().calc_text_size(value).x;
        let y = value_pos.y + fonts::heading().px() - fonts::small().px() - s(1.0);
        draw.text_font(fonts::small_semibold(), v2(value_pos.x + value_width + s(8.0), y), col(tone_color(change_tone), 1.0), change);
    }
    dummy(v2(width, height));
}

/// A smooth area chart with a hover readout; fills the row width. `min`/`max` default to the data range.
pub fn line_chart(ui: &Ui, id: &str, values: &[f32], height: f32, min: Option<f32>, max: Option<f32>, tone: Tone) {
    let height = if height <= 0.0 { s(120.0) } else { height };
    let width = content_region_avail().x;
    let start = cursor_screen_pos();
    invisible_button(id, v2(width, height));
    let hovered = is_item_hovered();
    let draw = Draw::window();
    let color = tone_color(tone);

    // three faint guide lines
    for i in 0..=2 {
        let y = start.y + s(4.0) + (height - s(8.0)) * i as f32 / 2.0;
        draw.line(v2(start.x, y), v2(start.x + width, y), col(p().border, 1.0), 1.0);
    }
    if values.len() < 2 {
        return;
    }
    let low = min.unwrap_or_else(|| values.iter().copied().fold(f32::MAX, f32::min));
    let mut high = max.unwrap_or_else(|| values.iter().copied().fold(f32::MIN, f32::max));
    if high - low < 1e-6 {
        high = low + 1.0;
    }
    let top = start.y + s(6.0);
    let bottom = start.y + height - s(6.0);
    let points: Vec<sys::ImVec2_c> = values
        .iter()
        .enumerate()
        .map(|(i, &value)| {
            let x = start.x + width * i as f32 / (values.len() - 1) as f32;
            let y = bottom - (bottom - top) * ((value - low) / (high - low)).clamp(0.0, 1.0);
            sys::ImVec2_c { x, y }
        })
        .collect();

    // The area is drawn as quads; anti-aliased fill is turned off so adjacent quads do not leave seams.
    unsafe {
        let flags = (*draw.0).Flags;
        (*draw.0).Flags &= !(sys::ImDrawFlags_AAFill as sys::ImDrawFlags);
        let fill = col(color, if theme::is_dark() { 0.12 } else { 0.10 });
        for pair in points.windows(2) {
            let (a, b) = (pair[0], pair[1]);
            sys::ImDrawList_AddQuadFilled(draw.0, a, b, sys::ImVec2_c { x: b.x, y: bottom }, sys::ImVec2_c { x: a.x, y: bottom }, fill);
        }
        (*draw.0).Flags = flags;
        sys::ImDrawList_AddPolyline(draw.0, points.as_ptr(), points.len() as i32, col(color, 1.0), s(2.0), 0);
    }

    if !hovered {
        return;
    }
    let mouse = unsafe { (*sys::igGetIO_Nil()).MousePos.x };
    let index = (((mouse - start.x) / width * (values.len() - 1) as f32).round() as isize).clamp(0, values.len() as isize - 1) as usize;
    let point: V2 = points[index].into();
    draw.line(v2(point.x, top), v2(point.x, bottom), col(p().text_faint, 0.6), 1.0);
    draw.circle_filled(point, s(5.0), col(color, 1.0));
    draw.circle_filled(point, s(2.5), col(p().background, 1.0));
    push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, s2(10.0, 6.0));
    if unsafe { sys::igBeginTooltip() } {
        text(ui, &format!("{:.1}", values[index]), p().text);
        unsafe { sys::igEndTooltip() };
    }
    pop_style_var(1);
}

// ---------------------------------------------------------------------------------------------------------- controls

fn check_size() -> f32 {
    s(14.0).round()
}

// The check's stroke runs from the end of its short leg to the tip of its long one as progress goes 0..1.
fn draw_check_mark(draw: Draw, min: V2, bx: f32, progress: f32) {
    let a = min + v2(bx * 0.26, bx * 0.53);
    let b = min + v2(bx * 0.43, bx * 0.70);
    let c = min + v2(bx * 0.75, bx * 0.33);
    let short_leg = (b.x - a.x).hypot(b.y - a.y);
    let long_leg = (c.x - b.x).hypot(c.y - b.y);
    let length = (short_leg + long_leg) * progress;
    let color = col(p().on_accent, 1.0);
    let thickness = s(1.75).max(1.5);
    if length <= short_leg {
        draw.line(a, a + (b - a) * (length / short_leg), color, thickness);
        return;
    }
    let tip = b + (c - b) * ((length - short_leg) / long_leg);
    unsafe {
        sys::ImDrawList_PathLineTo(draw.0, a.into());
        sys::ImDrawList_PathLineTo(draw.0, b.into());
        sys::ImDrawList_PathLineTo(draw.0, tip.into());
        sys::ImDrawList_PathStroke(draw.0, color, thickness, 0);
    }
}

// A square box with a hairline border; when on, it fills with ink and a check draws itself in.
fn draw_check(start: V2, height: f32, text: &str, value: bool, hovered: bool) {
    let p = p();
    let on = anim::toggle(get_id(c"##on"), value, 20.0);
    let hover = anim::toggle(get_id(c"##hover"), hovered, 18.0);
    let draw = Draw::window();
    let bx = check_size();
    let min = v2(start.x, (start.y + (height - bx) * 0.5).round());
    let ink = palette::lerp(p.accent, p.accent_hover, hover);
    let fill = palette::lerp(palette::lerp(p.surface, p.elevated, hover), ink, on);
    let border = palette::lerp(palette::lerp(p.border, p.text_faint, hover * 0.6), ink, on);
    draw.rect_filled(min, min + V2::splat(bx), col(fill, 1.0), s(3.0));
    draw.rect(min, min + V2::splat(bx), col(border, 1.0), s(3.0));
    if on > 0.01 {
        draw_check_mark(draw, min, bx, anim::ease_out_cubic(on));
    }
    if !text.is_empty() {
        draw.text(v2(min.x + bx + s(10.0), start.y + (height - text_line_height()) * 0.5), col(p.text, 1.0), text);
    }
}

/// A checkbox row: the box and its label, the whole row clickable. The settings-style boolean.
pub fn toggle(ui: &Ui, label: &str, value: &mut bool, description: Option<&str>) -> bool {
    push_id(label);
    let width = content_region_avail().x.max(1.0);
    let height = frame_height();
    let start = cursor_screen_pos();
    let changed = invisible_button("##row", v2(width, height));
    if changed {
        *value = !*value;
    }
    let hovered = is_item_hovered();
    if hovered {
        hand_cursor();
    }
    draw_check(start, height, visible(label), *value, hovered);
    pop_id();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// A compact checkbox sized to its label, for several on one line.
pub fn checkbox(_ui: &Ui, label: &str, value: &mut bool) -> bool {
    push_id(label);
    let text = visible(label);
    let height = frame_height();
    let width = check_size() + if text.is_empty() { 0.0 } else { s(10.0) + calc_text_size(text).x };
    let start = cursor_screen_pos();
    let changed = invisible_button("##box", v2(width, height));
    if changed {
        *value = !*value;
    }
    let hovered = is_item_hovered();
    if hovered {
        hand_cursor();
    }
    draw_check(start, height, text, *value, hovered);
    pop_id();
    changed
}

fn round_to(value: f32, decimals: i32) -> f32 {
    let scale = 10f64.powi(decimals);
    ((value as f64 * scale).round() / scale) as f32
}

// The width of the widest text the slider can show: min and max as shown, with every digit drawn as the widest one.
fn slider_text_width(format: &dyn Fn(f32) -> String, min: f32, max: f32) -> f32 {
    let mut widest = '0';
    let mut widest_width = 0.0;
    for digit in '0'..='9' {
        let digit_width = calc_text_size(digit.encode_utf8(&mut [0; 4])).x;
        if digit_width > widest_width {
            widest = digit;
            widest_width = digit_width;
        }
    }
    [min, max]
        .iter()
        .map(|&value| {
            let text: String = format(value).chars().map(|c| if c.is_ascii_digit() { widest } else { c }).collect();
            calc_text_size(&text).x
        })
        .fold(0.0, f32::max)
}

// The decimals the format shows (`{:.2}x` shows 2): the fewest at which rounding leaves the shown text unchanged.
// `None` when none up to 6 does, or when one shown step is wider than the range.
fn shown_decimals(format: &dyn Fn(f32) -> String, min: f32, max: f32) -> Option<i32> {
    let probes = [0.123_456_7, 0.456_789_1, 0.765_432_1];
    let decimals = (0..=6).find(|&decimals| {
        probes.iter().all(|probe| {
            let value = min + (max - min) * probe;
            format(round_to(value, decimals)) == format(value)
        })
    })?;
    (10f32.powi(-decimals) <= max - min).then_some(decimals)
}

#[allow(clippy::too_many_arguments)] // mirrors the C# and C++ signatures
fn slider_core(
    ui: &Ui,
    label: &str,
    value: &mut f32,
    min: f32,
    max: f32,
    step: f32,
    format: &dyn Fn(f32) -> String,
    description: Option<&str>,
) -> bool {
    let p = p();
    push_id(label);
    begin_group();
    label_line(label);
    let width = control_width(ui);
    let height = frame_height() + s(4.0);
    let start = cursor_screen_pos();
    let draw = Draw::window();

    // The value box, as wide as the widest text the slider can show: if it followed the current text, the track would
    // shift under a still cursor whenever the text changed width (Inter's digits are proportional).
    let shown = format(*value);
    let text_size = calc_text_size(&shown);
    let box_width = s(48.0).max(slider_text_width(format, min, max) + s(20.0)).max(text_size.x + s(20.0));
    draw.rect_filled(start, start + v2(box_width, height), col(p.surface, 1.0), s(4.0));
    draw.rect(start, start + v2(box_width, height), col(p.border, 1.0), s(4.0));
    draw.text(start + (v2(box_width, height) - text_size) * 0.5, col(p.text, 1.0), &shown);

    // the track: a thin rail, an ink fill up to a small grip
    let grip = s2(22.0, 15.0);
    let gap_x = s(30.0);
    let track_start = v2(start.x + box_width + gap_x, start.y);
    let track_width = s(40.0).max(width - box_width - gap_x);
    set_cursor_screen_pos(track_start);
    invisible_button("##track", v2(track_width, height));
    let active = is_item_active();
    let hovered = is_item_hovered();
    if hovered || active {
        hand_cursor();
    }
    let left = track_start.x + grip.x * 0.5;
    let right = track_start.x + track_width - grip.x * 0.5;
    let mut changed = false;
    if (active || hovered) && max > min {
        let io = unsafe { &*sys::igGetIO_Nil() };
        let storage = unsafe { sys::igGetStateStorage() };
        let grab_id = get_id(c"##grab");
        let from_mouse = |x: f32| min + ((x - left) / (right - left)).clamp(0.0, 1.0) * (max - min);
        let mut next = None;
        if unsafe { sys::igIsItemActivated() } {
            // Pressing the grip keeps the value and drags from where it was grabbed; pressing the rail jumps there.
            let grip_center = anim::lerp(left, right, ((*value - min) / (max - min)).clamp(0.0, 1.0));
            let on_grip = (io.MousePos.x - grip_center).abs() <= grip.x * 0.5;
            unsafe { sys::ImGuiStorage_SetFloat(storage, grab_id, if on_grip { io.MousePos.x - grip_center } else { 0.0 }) };
            if !on_grip {
                next = Some(from_mouse(io.MousePos.x));
            }
        } else if active && io.MouseDelta.x != 0.0 {
            next = Some(from_mouse(io.MousePos.x - unsafe { sys::ImGuiStorage_GetFloat(storage, grab_id, 0.0) }));
        }
        // Left/Right step by one shown unit, for values the track has too few pixels to land on.
        let keys = active || !io.WantTextInput;
        let pressed = |key| keys && unsafe { sys::igIsKeyPressed_Bool(key, true) };
        let direction = pressed(sys::ImGuiKey_RightArrow) as i32 - pressed(sys::ImGuiKey_LeftArrow) as i32;
        if next.is_some() || direction != 0 {
            // Values snap to what the box shows, so the stored value is the one on screen and equal-looking values
            // cannot alternate.
            let decimals = if step > 0.0 { None } else { shown_decimals(format, min, max) };
            let unit = if step > 0.0 { step } else { decimals.map_or((max - min) / 100.0, |d| 10f32.powi(-d)) };
            let mut candidate = next.unwrap_or(*value) + direction as f32 * unit;
            if step > 0.0 {
                candidate = (candidate / step).round() * step;
            } else if let Some(decimals) = decimals {
                candidate = round_to(candidate, decimals);
            }
            candidate = candidate.clamp(min, max);
            if candidate != *value {
                *value = candidate;
                changed = true;
            }
        }
    }
    let target = if max > min { ((*value - min) / (max - min)).clamp(0.0, 1.0) } else { 0.0 };
    let t = anim::to(get_id(c"##fill"), target, if active { 40.0 } else { 16.0 }, None);
    let hover = anim::toggle(get_id(c"##hover"), hovered || active, 18.0);

    let cy = (track_start.y + height * 0.5).round();
    let thick = s(8.0).round();
    let rail_min = v2(track_start.x, cy - thick * 0.5);
    let rail_max = v2(track_start.x + track_width, cy + thick * 0.5);
    draw.rect_filled(rail_min, rail_max, col(p.surface, 1.0), thick * 0.5);
    draw.rect(rail_min, rail_max, col(p.border, 1.0), thick * 0.5);
    let grip_x = anim::lerp(left, right, t);
    if grip_x - rail_min.x > s(2.0) {
        draw.rect_filled(
            rail_min + s2(1.0, 1.0),
            v2(grip_x, rail_max.y - s(1.0)),
            col(palette::lerp(p.accent, p.accent_hover, hover), 1.0),
            thick * 0.5,
        );
    }
    let grip_min = v2((grip_x - grip.x * 0.5).round(), (cy - grip.y * 0.5).round());
    draw.rect_filled(
        grip_min + v2(0.0, s(1.0)),
        grip_min + grip + v2(0.0, s(1.0)),
        col([0.0, 0.0, 0.0, if theme::is_dark() { 0.3 } else { 0.08 }], 1.0),
        s(4.0),
    );
    draw.rect_filled(grip_min, grip_min + grip, col(p.knob, 1.0), s(4.0));
    draw.rect(grip_min, grip_min + grip, col(palette::lerp(p.border, p.text_faint, hover), 1.0), s(4.0));
    // three grip lines
    for i in -1..=1 {
        let lx = (grip_min.x + grip.x * 0.5 + i as f32 * s(3.0)).round() + 0.5;
        draw.line(v2(lx, grip_min.y + s(4.5)), v2(lx, grip_min.y + grip.y - s(4.5)), col(p.text_muted, 1.0), s(1.0).max(1.0));
    }

    set_cursor_screen_pos(start);
    dummy(v2(width, height));
    end_group();
    pop_id();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// The label above, the value in a box and the track beside it. `format` renders the value: `|v| format!("{v:.2}")`;
/// the value snaps to the precision it shows. Left/Right step by one shown unit while the slider is hovered or dragged.
pub fn slider(
    ui: &Ui,
    label: &str,
    value: &mut f32,
    min: f32,
    max: f32,
    format: impl Fn(f32) -> String,
    description: Option<&str>,
) -> bool {
    slider_core(ui, label, value, min, max, 0.0, &format, description)
}

/// Integer slider; snaps to whole numbers.
pub fn slider_int(ui: &Ui, label: &str, value: &mut i32, min: i32, max: i32, description: Option<&str>) -> bool {
    let mut f = *value as f32;
    if !slider_core(ui, label, &mut f, min as f32, max as f32, 1.0, &|v: f32| (v.round() as i32).to_string(), description) {
        return false;
    }
    *value = f.round() as i32;
    true
}

/// A button with a smooth hover transition. `size.x`: 0 fits the text, < 0 fills the row; `size.y` 0 is the frame
/// height. Icons concatenate: `format!("{}  Save", icons::SAVE)`.
pub fn button(_ui: &Ui, label: &str, kind: ButtonKind, size: [f32; 2]) -> bool {
    let p = p();
    let text = visible(label);
    let text_size = calc_text_size(text);
    let padding: V2 = style().FramePadding.into();
    let mut size = V2::from(size);
    if size.x < 0.0 {
        size.x = content_region_avail().x.max(1.0);
    }
    if size.x == 0.0 {
        size.x = text_size.x + padding.x * 2.0 + s(10.0);
    }
    if size.y == 0.0 {
        size.y = frame_height();
    }

    let start = cursor_screen_pos();
    let clicked = invisible_button(label, size);
    let hovered = is_item_hovered();
    let id = item_id();
    let hover = anim::toggle(id ^ 0x5A17, hovered, 18.0);
    let press = anim::toggle(id ^ 0x7E11, is_item_active(), 28.0);
    if hovered {
        hand_cursor();
    }

    let (background, foreground, border) = match kind {
        ButtonKind::Primary => {
            (palette::lerp(palette::lerp(p.accent, p.accent_hover, hover), p.accent, press), p.on_accent, palette::alpha(p.accent, 0.0))
        }
        ButtonKind::Ghost => (
            palette::lerp(palette::alpha(p.field, 0.0), p.field, hover.max(press)),
            palette::lerp(p.text_muted, p.text, hover),
            palette::alpha(p.border, 0.0),
        ),
        ButtonKind::Danger => {
            (palette::alpha(p.danger, anim::lerp(0.06, 0.12, hover) + 0.06 * press), p.danger, palette::alpha(p.danger, 0.35))
        }
        ButtonKind::Secondary => (
            palette::lerp(palette::lerp(p.surface, p.elevated, hover), p.field, press),
            p.text,
            palette::lerp(p.border, p.text_faint, hover * 0.7),
        ),
    };
    let draw = Draw::window();
    let rounding = style().FrameRounding;
    draw.rect_filled(start, start + size, col(background, 1.0), rounding);
    if border[3] > 0.0 {
        draw.rect(start, start + size, col(border, 1.0), rounding);
    }
    draw.text(start + (size - text_size) * 0.5, col(foreground, 1.0), text);
    clicked
}

/// A square, borderless icon button. `id` keeps it unique when the same icon repeats.
pub fn icon_button(ui: &Ui, icon: &str, id: &str, tooltip_text: Option<&str>, size: f32) -> bool {
    let size = if size <= 0.0 { frame_height() } else { size };
    let start = cursor_screen_pos();
    let clicked = invisible_button(id, V2::splat(size));
    let hovered = is_item_hovered();
    let hover = anim::toggle(item_id() ^ 0x1C0B, hovered, 18.0);
    if hovered {
        hand_cursor();
    }
    let draw = Draw::window();
    draw.rect_filled(start, start + V2::splat(size), col(p().field, hover), style().FrameRounding);
    let icon_size = calc_text_size(icon);
    draw.text(start + (V2::splat(size) - icon_size) * 0.5, col(palette::lerp(p().text_muted, p().text, hover), 1.0), icon);
    if let Some(tip) = tooltip_text {
        tooltip(ui, tip);
    }
    clicked
}

/// A segmented control with a sliding ink thumb. `width` 0: [`control_width`].
pub fn segmented(ui: &Ui, id: &str, index: &mut usize, items: &[&str], width: f32) -> bool {
    if items.is_empty() {
        return false;
    }
    let p = p();
    push_id(id);
    let width = if width <= 0.0 { control_width(ui).max(1.0) } else { width };
    let height = frame_height();
    let start = cursor_screen_pos();
    let draw = Draw::window();
    let rounding = style().FrameRounding;
    let inset = s(3.0);
    let segment = (width - inset * 2.0) / items.len() as f32;
    draw.rect_filled(start, start + v2(width, height), col(p.surface, 1.0), rounding);
    draw.rect(start, start + v2(width, height), col(p.border, 1.0), rounding);

    let at = anim::to(get_id(c"##thumb"), *index as f32, 16.0, None);
    let thumb_min = v2(start.x + inset + segment * at, start.y + inset);
    draw.rect_filled(thumb_min, thumb_min + v2(segment, height - inset * 2.0), col(p.accent, 1.0), (rounding - inset * 0.5).max(0.0));

    let mut changed = false;
    for (i, item) in items.iter().enumerate() {
        let item_min = v2(start.x + inset + segment * i as f32, start.y);
        set_cursor_screen_pos(item_min);
        push_id_int(i as i32);
        if invisible_button("##seg", v2(segment, height)) && *index != i {
            *index = i;
            changed = true;
        }
        pop_id();
        let hovered = is_item_hovered();
        if hovered {
            hand_cursor();
        }
        let selected = (1.0 - (at - i as f32).abs()).clamp(0.0, 1.0);
        let hover = anim::toggle(item_id(), hovered, 18.0);
        let color = palette::lerp(palette::lerp(p.text_muted, p.text, hover), p.on_accent, selected);
        let text = visible(item);
        let size = calc_text_size(text);
        draw.text(v2(item_min.x + (segment - size.x) * 0.5, item_min.y + (height - size.y) * 0.5), col(color, 1.0), text);
    }
    set_cursor_screen_pos(start);
    dummy(v2(width, height));
    pop_id();
    changed
}

/// Width of a control under its label (the column width, up to 280 px).
pub fn control_width(_ui: &Ui) -> f32 {
    s(280.0).min(content_region_avail().x)
}

/// A label on its own line; leaves the cursor and item width where the control goes, so any ImGui widget fits the
/// layout: `ui::row(ui, "Mode", 0.0); ui.slider(...)`.
pub fn row(ui: &Ui, label: &str, width: f32) {
    let width = if width <= 0.0 { control_width(ui) } else { width };
    label_line(label);
    unsafe { sys::igSetNextItemWidth(width) };
}

/// A labeled dropdown; the list opens just under the field, the selected item highlighted.
pub fn combo(ui: &Ui, label: &str, index: &mut usize, items: &[&str], description: Option<&str>) -> bool {
    let p = p();
    push_id(label);
    begin_group();
    row(ui, label, 0.0);
    let width = unsafe { sys::igCalcItemWidth() };
    let size = v2(width, frame_height());
    let min = cursor_screen_pos();
    let max = min + size;
    if invisible_button("##field", size) {
        unsafe { sys::igOpenPopup_Str(c"##list".as_ptr(), 0) };
    }
    let hovered = is_item_hovered();
    if hovered {
        hand_cursor();
    }
    let open = unsafe { sys::igIsPopupOpen_Str(c"##list".as_ptr(), 0) };
    let hover = anim::toggle(get_id(c"##hover"), hovered || open, 18.0);

    let draw = Draw::window();
    let rounding = style().FrameRounding;
    let padding: V2 = style().FramePadding.into();
    draw.rect_filled(min, max, col(palette::lerp(p.surface, p.elevated, hover), 1.0), rounding);
    draw.rect(min, max, col(palette::lerp(p.border, p.text_faint, hover * 0.7), 1.0), rounding);
    let preview = items.get(*index).copied().unwrap_or("");
    let text_y = min.y + (size.y - text_line_height()) * 0.5;
    draw.push_clip_rect(min, max - v2(padding.x + s(16.0), 0.0));
    draw.text(v2(min.x + padding.x, text_y), col(p.text, 1.0), preview);
    draw.pop_clip_rect();
    let chevron = if open { icons::CHEVRON_UP } else { icons::CHEVRON_DOWN };
    let chevron_size = calc_text_size(chevron);
    draw.text(v2(max.x - chevron_size.x - padding.x, min.y + (size.y - chevron_size.y) * 0.5), col(p.text_muted, 1.0), chevron);

    let mut changed = false;
    unsafe {
        sys::igSetNextWindowPos(v2(min.x, max.y + s(6.0)).into(), 0, v2(0.0, 0.0).into());
        sys::igSetNextWindowSize(v2(width, 0.0).into(), 0);
    }
    push_style_var(sys::ImGuiStyleVar_PopupRounding, s(5.0));
    push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, s2(6.0, 6.0));
    push_style_var_v2(sys::ImGuiStyleVar_ItemSpacing, v2(0.0, s(2.0)));
    push_style_var_v2(sys::ImGuiStyleVar_SelectableTextAlign, v2(0.0, 0.5));
    if unsafe { sys::igBeginPopup(c"##list".as_ptr(), sys::ImGuiWindowFlags_NoMove as _) } {
        for (i, item) in items.iter().enumerate() {
            let selected = i == *index;
            push_style_color(sys::ImGuiCol_Text, if selected { p.text } else { p.text_muted });
            push_id_int(i as i32);
            let text = cstring(&format!("  {item}"));
            if unsafe { sys::igSelectable_Bool(text.as_ptr(), selected, 0, v2(0.0, s(28.0)).into()) } {
                *index = i;
                changed = true;
            }
            pop_id();
            pop_style_color(1);
            if selected && unsafe { sys::igIsWindowAppearing() } {
                unsafe { sys::igSetItemDefaultFocus() };
            }
        }
        unsafe { sys::igEndPopup() };
    }
    pop_style_var(4);
    end_group();
    pop_id();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// A labeled text field with placeholder text; at most `max_length` bytes.
pub fn text_input(ui: &Ui, label: &str, value: &mut String, hint_text: &str, max_length: usize, description: Option<&str>) -> bool {
    begin_group();
    row(ui, label, 0.0);
    let id = cstring(&format!("##{label}"));
    let hint_text = cstring(hint_text);
    let mut buffer = vec![0u8; max_length.max(value.len()) + 1];
    buffer[..value.len()].copy_from_slice(value.as_bytes());
    let changed = unsafe {
        sys::igInputTextWithHint(id.as_ptr(), hint_text.as_ptr(), buffer.as_mut_ptr().cast(), buffer.len(), 0, None, std::ptr::null_mut())
    };
    if changed {
        let end = buffer.iter().position(|&b| b == 0).unwrap_or(buffer.len());
        *value = String::from_utf8_lossy(&buffer[..end]).into_owned();
    }
    end_group();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// Drag or double-click to type. `min == max == 0`: unbounded. `format` is printf-style ("%.2f").
#[allow(clippy::too_many_arguments)] // mirrors the C# and C++ signatures
pub fn number(ui: &Ui, label: &str, value: &mut f32, speed: f32, min: f32, max: f32, format: &str, description: Option<&str>) -> bool {
    begin_group();
    row(ui, label, 0.0);
    let id = cstring(&format!("##{label}"));
    let format = cstring(format);
    let changed = unsafe { sys::igDragFloat(id.as_ptr(), value, speed, min, max, format.as_ptr(), 0) };
    end_group();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// Integer variant of [`number`].
pub fn number_int(ui: &Ui, label: &str, value: &mut i32, speed: f32, min: i32, max: i32, description: Option<&str>) -> bool {
    begin_group();
    row(ui, label, 0.0);
    let id = cstring(&format!("##{label}"));
    let changed = unsafe { sys::igDragInt(id.as_ptr(), value, speed, min, max, c"%d".as_ptr(), 0) };
    end_group();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// A swatch with its label beside it; the swatch opens a picker.
pub fn color(ui: &Ui, label: &str, value: &mut Color, alpha: bool, description: Option<&str>) -> bool {
    let swatch = frame_height();
    begin_group();
    let mut flags = sys::ImGuiColorEditFlags_NoInputs | sys::ImGuiColorEditFlags_NoLabel | sys::ImGuiColorEditFlags_AlphaPreviewHalf;
    flags |= if alpha { sys::ImGuiColorEditFlags_AlphaBar } else { sys::ImGuiColorEditFlags_NoAlpha };
    push_style_var_v2(sys::ImGuiStyleVar_FramePadding, v2(swatch * 0.8 - s(1.0), style().FramePadding.y));
    let id = cstring(&format!("##{label}"));
    let changed = unsafe { sys::igColorEdit4(id.as_ptr(), value.as_mut_ptr(), flags as _) };
    pop_style_var(1);
    same_line(0.0, s(12.0));
    unsafe { sys::igAlignTextToFramePadding() };
    text_unformatted(visible(label));
    end_group();
    about_with_title(ui, label, description);
    gap();
    changed
}

/// An outlined button reading "Label: Key". Click, then press a key or mouse button. Esc cancels, Backspace clears
/// (0). `key` is a Windows virtual-key code (see [`input::vk`]).
pub fn keybind(ui: &Ui, label: &str, key: &mut i32, description: Option<&str>) -> bool {
    let id = get_id_str(label);
    let waiting = CAPTURING_KEY.get() == id;
    let mut changed = false;
    if waiting {
        let pressed = input::any_pressed();
        if pressed == input::vk::ESCAPE {
            CAPTURING_KEY.set(0);
        } else if pressed == input::vk::BACK {
            *key = 0;
            changed = true;
            CAPTURING_KEY.set(0);
        } else if pressed != 0 {
            *key = pressed;
            changed = true;
            CAPTURING_KEY.set(0);
        }
    }
    let shown = if waiting { "press a key...".to_string() } else { input::key_name(*key) };
    let text = format!("{}: {}##{}", visible(label), shown, label);
    let kind = if waiting { ButtonKind::Primary } else { ButtonKind::Secondary };
    if button(ui, &text, kind, [control_width(ui), 0.0]) && !waiting {
        CAPTURING_KEY.set(id);
    }
    about_with_title(ui, label, description);
    gap();
    changed
}

// ---------------------------------------------------------------------------------------------------------- feedback

/// A thin animated progress bar. `label` shows on the left, the percentage on the right.
pub fn progress(ui: &Ui, fraction: f32, label: Option<&str>, tone: Tone, height: f32) {
    let height = if height <= 0.0 { s(6.0) } else { height };
    let width = content_region_avail().x;
    push_id(label.unwrap_or("##progress"));
    if let Some(label) = label {
        let x0 = unsafe { sys::igGetCursorPosX() };
        text_unformatted(visible(label));
        let percent = format!("{:.0}%", fraction.clamp(0.0, 1.0) * 100.0);
        same_line(x0 + width - calc_text_size(&percent).x, -1.0);
        text(ui, &percent, p().text_muted);
    }
    let shown = anim::to(get_id(c"##value"), fraction.clamp(0.0, 1.0), 10.0, Some(0.0));
    let start = cursor_screen_pos();
    let draw = Draw::window();
    draw.rect_filled(start, start + v2(width, height), col(p().field, 1.0), height);
    if shown > 0.001 {
        draw.rect_filled(start, start + v2(height.max(width * shown), height), col(tone_color(tone), 1.0), height);
    }
    dummy(v2(width, height));
    pop_id();
}

/// An indeterminate loading spinner.
pub fn spinner(_ui: &Ui, radius: f32, thickness: f32) {
    let radius = if radius <= 0.0 { s(8.0) } else { radius };
    let thickness = if thickness <= 0.0 { s(2.0) } else { thickness };
    let start = cursor_screen_pos();
    let center = start + V2::splat(radius + thickness);
    let time = unsafe { sys::igGetTime() } as f32;
    let draw = Draw::window();
    draw.circle(center, radius, col(p().field, 1.0), thickness);
    let head = time * 5.5;
    let length = 1.2 + (time * 2.4).sin() * 0.7;
    unsafe {
        sys::ImDrawList_PathArcTo(draw.0, center.into(), radius, head, head + length, 24);
        sys::ImDrawList_PathStroke(draw.0, col(p().accent, 1.0), thickness, 0);
    }
    dummy(V2::splat((radius + thickness) * 2.0));
}

/// A fading tooltip for the previous item. Call right after the widget it describes.
pub fn tooltip(_ui: &Ui, text: &str) {
    let id = item_id() ^ 0x7007;
    if !unsafe { sys::igIsItemHovered(sys::ImGuiHoveredFlags_ForTooltip as _) } {
        anim::set(id, 0.0);
        return;
    }
    let alpha = anim::to(id, 1.0, 16.0, Some(0.0));
    push_style_var(sys::ImGuiStyleVar_Alpha, style().Alpha * alpha);
    push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, s2(10.0, 7.0));
    push_style_var(sys::ImGuiStyleVar_PopupRounding, s(5.0));
    if unsafe { sys::igBeginTooltip() } {
        unsafe { sys::igPushTextWrapPos(s(280.0)) };
        text_unformatted(text);
        unsafe {
            sys::igPopTextWrapPos();
            sys::igEndTooltip();
        }
    }
    pop_style_var(3);
}

/// A muted info icon with a tooltip; use after `ui.same_line()`.
pub fn help_marker(ui: &Ui, text: &str) {
    self::text(ui, icons::INFO, p().text_faint);
    tooltip(ui, text);
}
