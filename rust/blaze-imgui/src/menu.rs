//! The menu window, in four columns: an icon rail with the brand mark, the page's title and tabs with the brand footer,
//! the selected tab's controls, and a status column with live readouts and an ABOUT hint for the hovered control.
//! The rail tile and tab highlight glide; content fades in when the page or tab changes.
//!
//! Pages and tabs are closures over your application state `S`: `menu.draw(ui, &mut state, ..)` lends the state to
//! whichever closures run this frame.

use std::cell::Cell;

use dear_imgui_rs::{sys, Ui};

use crate::anim;
use crate::fonts;
use crate::math::{v2, V2};
use crate::theme;
use crate::ui::{self, cstring, Draw};

/// Draws a tab body or a status column.
pub type DrawFn<S> = Box<dyn FnMut(&Ui, &mut S)>;

thread_local! {
    static CLOSE_REQUESTED: Cell<bool> = const { Cell::new(false) };
}

/// Asks the host to hide the menu, e.g. from a "Close menu" button inside a tab. The overlay hides it on the next
/// frame; without the overlay, read [`Menu::take_close_request`].
pub fn request_close() {
    CLOSE_REQUESTED.set(true);
}

/// One entry of a page's tab list and the content drawn when it is selected.
pub struct MenuTab<S> {
    pub title: String,
    pub draw: DrawFn<S>,
    /// Under ABOUT in the status column until a control with a description is hovered.
    pub about: Option<String>,
}

/// One icon on the rail: a titled page with its own tabs and, optionally, its own status readout.
pub struct MenuPage<S> {
    pub title: String,
    pub icon: String,
    /// Quiet uppercase line under the page title ("ASSIST").
    pub caption: Option<String>,
    /// The status column for this page; `None` falls back to [`Menu::status`].
    pub status: Option<DrawFn<S>>,
    /// Icon at the bottom of the rail (settings).
    pub pin_bottom: bool,
    /// A small brand dot on the rail icon.
    pub badge: bool,
    /// false: the tabs get the whole body without scrolling and lay themselves out.
    pub scroll: bool,
    tabs: Vec<MenuTab<S>>,
    selected_tab: usize,
    tab_changed: bool,
}

impl<S> MenuPage<S> {
    /// Adds a tab; `about` shows under ABOUT until a control with a description is hovered.
    pub fn add_tab(&mut self, title: &str, draw: impl FnMut(&Ui, &mut S) + 'static, about: Option<&str>) -> &mut Self {
        self.tabs.push(MenuTab { title: title.into(), draw: Box::new(draw), about: about.map(Into::into) });
        self
    }

    pub fn tabs(&self) -> &[MenuTab<S>] {
        &self.tabs
    }

    pub fn selected_tab(&self) -> usize {
        self.selected_tab
    }

    /// Selects a tab and plays the content transition.
    pub fn set_selected_tab(&mut self, index: usize) {
        let index = index.min(self.tabs.len().saturating_sub(1));
        if index != self.selected_tab {
            self.selected_tab = index;
            self.tab_changed = true;
        }
    }

    pub fn select_tab(&mut self, title: &str) {
        if let Some(index) = self.tabs.iter().position(|t| t.title == title) {
            self.set_selected_tab(index);
        }
    }

    pub fn set_status(&mut self, status: impl FnMut(&Ui, &mut S) + 'static) -> &mut Self {
        self.status = Some(Box::new(status));
        self
    }
}

pub struct Menu<S> {
    /// Brand name at the bottom of the tab column.
    pub title: String,
    /// Small muted word after it.
    pub subtitle: Option<String>,
    /// After the subtitle ("v1.0.0").
    pub version: Option<String>,
    /// The rail's brand mark; `None`: the first letter of the title.
    pub logo: Option<String>,
    /// Initial size before scale.
    pub size: [f32; 2],
    pub min_size: [f32; 2],
    pub resizable: bool,
    pub rail_width: f32,
    pub nav_width: f32,
    /// The status column; the controls column takes the rest.
    pub status_width: f32,
    /// Without the status column the controls take the rest and descriptions show under their controls.
    pub show_status: bool,
    pub status_title: String,
    /// The status column for pages without their own.
    pub status: Option<DrawFn<S>>,
    pub about_title: String,
    pages: Vec<MenuPage<S>>,
    selected: usize,
    page_progress: f32,
    nav_progress: f32,
    about_progress: f32,
    about_shown: (String, String),
    rest_position: Option<V2>,
    // Where the user put the window, where it was last frame, and the viewport work area it was last fitted to
    // (each a position and a size).
    wanted: Option<(V2, V2)>,
    placed: Option<(V2, V2)>,
    work_area: Option<(V2, V2)>,
    center_pending: bool,
    window_center: V2,
    close_requested: bool,
}

impl<S> Default for Menu<S> {
    fn default() -> Self {
        Menu {
            title: "Blaze".into(),
            subtitle: None,
            version: None,
            logo: None,
            size: [1000.0, 600.0],
            min_size: [860.0, 500.0],
            resizable: true,
            rail_width: 62.0,
            nav_width: 200.0,
            status_width: 280.0,
            show_status: true,
            status_title: "Status".into(),
            status: None,
            about_title: "About".into(),
            pages: Vec::new(),
            selected: 0,
            page_progress: 1.0,
            nav_progress: 1.0,
            about_progress: 1.0,
            about_shown: (String::new(), String::new()),
            rest_position: None,
            wanted: None,
            placed: None,
            work_area: None,
            center_pending: false,
            window_center: V2::ZERO,
            close_requested: false,
        }
    }
}

fn pad() -> f32 {
    18.0 * ui::scale()
}

fn title_y() -> f32 {
    17.0 * ui::scale()
}

fn body_top() -> f32 {
    54.0 * ui::scale()
}

// The ABOUT text's bottom edge above the window's, level with the brand footer.
fn about_bottom() -> f32 {
    30.0 * ui::scale()
}

// A span already inside lo..hi stays put; otherwise it moves in, a margin from the edge when there is room.
fn keep_inside(pos: f32, size: f32, lo: f32, hi: f32, margin: f32) -> f32 {
    if pos >= lo && pos + size <= hi {
        return pos;
    }
    let margin = ((hi - lo - size) * 0.5).clamp(0.0, margin);
    (lo + margin).max(pos.min(hi - size - margin))
}

// A soft shadow under the window, drawn behind every ImGui window.
fn draw_shadow(origin: V2, size: V2) {
    let draw = Draw::background();
    let scale = ui::scale();
    let rounding = ui::style().WindowRounding;
    let strength = if theme::is_dark() { 0.09 } else { 0.028 };
    for i in 1..=10 {
        let grow = i as f32 * 2.4 * scale;
        let offset = v2(0.0, 6.0 * scale);
        draw.rect_filled(
            origin - V2::splat(grow) + offset,
            origin + size + V2::splat(grow) + offset,
            theme::u32([0.0, 0.0, 0.0, strength * (1.0 - i as f32 / 11.0)], 1.0),
            rounding + grow,
        );
    }
}

impl<S> Menu<S> {
    pub fn new() -> Self {
        Self::default()
    }

    /// A rail page; give it tabs with [`MenuPage::add_tab`].
    pub fn add_page(&mut self, title: &str, icon: &str, caption: Option<&str>) -> &mut MenuPage<S> {
        self.pages.push(MenuPage {
            title: title.into(),
            icon: icon.into(),
            caption: caption.map(Into::into),
            status: None,
            pin_bottom: false,
            badge: false,
            scroll: true,
            tabs: Vec::new(),
            selected_tab: 0,
            tab_changed: false,
        });
        self.pages.last_mut().expect("just pushed")
    }

    pub fn pages(&self) -> &[MenuPage<S>] {
        &self.pages
    }

    pub fn page_mut(&mut self, index: usize) -> Option<&mut MenuPage<S>> {
        self.pages.get_mut(index)
    }

    pub fn set_status(&mut self, status: impl FnMut(&Ui, &mut S) + 'static) {
        self.status = Some(Box::new(status));
    }

    pub fn selected(&self) -> usize {
        self.selected
    }

    /// Selects a page and plays the page transition.
    pub fn set_selected(&mut self, index: usize) {
        let index = index.min(self.pages.len().saturating_sub(1));
        if index == self.selected {
            return;
        }
        self.selected = index;
        self.page_progress = 0.0;
        self.nav_progress = 0.0;
        self.reset_about();
    }

    /// The first page with this title; "Page/Tab" also selects the tab.
    pub fn select(&mut self, title: &str) {
        let (page_title, tab_title) = match title.split_once('/') {
            Some((page, tab)) => (page, Some(tab)),
            None => (title, None),
        };
        if let Some(index) = self.pages.iter().position(|p| p.title == page_title) {
            self.set_selected(index);
            if let Some(tab) = tab_title {
                self.pages[index].select_tab(tab);
            }
        }
    }

    /// Puts the window back in the middle of the screen (next draw).
    pub fn center(&mut self) {
        self.center_pending = true;
    }

    /// The window's middle, as of the last draw.
    pub fn window_center(&self) -> [f32; 2] {
        self.window_center.into()
    }

    /// True once after a tab called [`request_close`].
    pub fn take_close_request(&mut self) -> bool {
        std::mem::take(&mut self.close_requested)
    }

    fn current_tab(&self) -> Option<&MenuTab<S>> {
        self.pages.get(self.selected).and_then(|p| p.tabs.get(p.selected_tab))
    }

    fn reset_about(&self) {
        ui::set_current_about(self.current_tab().and_then(|t| t.about.as_deref()).unwrap_or(""));
        ui::set_current_about_title("");
    }

    /// Draws the window. `visibility`: the open/close fade (0..1); `interactive`: false while fading out.
    pub fn draw(&mut self, ui: &Ui, state: &mut S, visibility: f32, interactive: bool) {
        let scale = ui::scale();
        let p = ui::p();
        if CLOSE_REQUESTED.replace(false) {
            self.close_requested = true;
        }
        let (work_pos, work_size): (V2, V2) = unsafe {
            let viewport = &*sys::igGetMainViewport();
            (viewport.WorkPos.into(), viewport.WorkSize.into())
        };

        // Never larger than the screen (games run at any resolution).
        let min_size = v2((self.min_size[0] * scale).min(work_size.x), (self.min_size[1] * scale).min(work_size.y));
        let max_size = v2(min_size.x.max(work_size.x), min_size.y.max(work_size.y));
        unsafe {
            sys::igSetNextWindowSize(
                v2((self.size[0] * scale).min(work_size.x * 0.98), (self.size[1] * scale).min(work_size.y * 0.98)).into(),
                sys::ImGuiCond_FirstUseEver as _,
            );
            sys::igSetNextWindowSizeConstraints(min_size.into(), max_size.into(), None, std::ptr::null_mut());
        }

        // imgui.ini may restore a place saved for another screen size, and the screen can change size at any time:
        // the menu then goes back to where the user put it, as far as it fits, instead of hanging off the edge or
        // staying shrunk after a small window.
        let mut refit = false;
        if let Some((wanted_pos, wanted_size)) = self.wanted {
            if self.work_area != Some((work_pos, work_size)) {
                refit = true;
                self.work_area = Some((work_pos, work_size));
                let fitted = v2(
                    wanted_size.x.clamp(min_size.x, max_size.x),
                    wanted_size.y.clamp(min_size.y, max_size.y),
                );
                let end = work_pos + work_size;
                let margin = 12.0 * scale;
                self.rest_position = Some(v2(
                    keep_inside(wanted_pos.x, fitted.x, work_pos.x, end.x, margin),
                    keep_inside(wanted_pos.y, fitted.y, work_pos.y, end.y, margin),
                ));
                unsafe { sys::igSetNextWindowSize(fitted.into(), sys::ImGuiCond_Always as _) };
            }
        }

        // The window rises slightly while it fades in; once open the user can drag it freely.
        let rise = (1.0 - anim::ease_out_cubic(visibility)) * 14.0 * scale;
        let center = work_pos + work_size * 0.5;
        unsafe {
            if self.center_pending {
                sys::igSetNextWindowPos(center.into(), sys::ImGuiCond_Always as _, v2(0.5, 0.5).into());
                self.rest_position = None;
                self.center_pending = false;
            } else if let (Some(rest), true) = (self.rest_position, visibility < 1.0 || refit) {
                sys::igSetNextWindowPos((rest + v2(0.0, rise)).into(), sys::ImGuiCond_Always as _, v2(0.0, 0.0).into());
            } else if self.rest_position.is_none() {
                sys::igSetNextWindowPos(center.into(), sys::ImGuiCond_FirstUseEver as _, v2(0.5, 0.5).into());
            }
        }

        let mut flags = sys::ImGuiWindowFlags_NoTitleBar
            | sys::ImGuiWindowFlags_NoCollapse
            | sys::ImGuiWindowFlags_NoScrollbar
            | sys::ImGuiWindowFlags_NoScrollWithMouse;
        if !self.resizable {
            flags |= sys::ImGuiWindowFlags_NoResize;
        }
        if !interactive {
            flags |= sys::ImGuiWindowFlags_NoInputs;
        }
        ui::push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, V2::ZERO);
        ui::push_style_var(sys::ImGuiStyleVar_WindowBorderSize, 1.0);
        let name = cstring(&format!("{}##BlazeMenu", self.title));
        unsafe { sys::igBegin(name.as_ptr(), std::ptr::null_mut(), flags as _) };
        ui::pop_style_var(2);

        let origin: V2 = unsafe { sys::igGetWindowPos().into() };
        let size: V2 = unsafe { sys::igGetWindowSize().into() };
        if visibility >= 1.0 || self.rest_position.is_none() {
            self.rest_position = Some(origin);
        }
        // A move or resize by the user (anything but a refit) becomes the place to go back to.
        let now = (self.rest_position.unwrap_or(origin), size);
        if !refit && self.placed != Some(now) {
            self.wanted = Some(now);
        }
        self.placed = Some(now);
        self.window_center = origin + size * 0.5;
        draw_shadow(origin, size);

        // A tab switched from code (or a click) since the last frame replays the content transition.
        if let Some(page) = self.pages.get_mut(self.selected) {
            if std::mem::take(&mut page.tab_changed) {
                self.page_progress = 0.0;
                self.reset_about();
            }
        }
        self.page_progress = (self.page_progress + anim::delta() / 0.26).min(1.0);
        self.nav_progress = (self.nav_progress + anim::delta() / 0.26).min(1.0);

        let rail = self.rail_width * scale;
        let nav = self.nav_width * scale;
        let content = size.x - rail - nav - if self.show_status { self.status_width * scale } else { 0.0 };
        let draw = Draw::window();
        let lines = [rail, rail + nav, rail + nav + content];
        for &x in &lines[..if self.show_status { 3 } else { 2 }] {
            draw.line(origin + v2(x, 0.0), origin + v2(x, size.y), theme::u32(p.border, 1.0), 1.0);
        }

        self.draw_rail(rail, size.y);
        self.draw_nav(rail, nav, size.y);
        ui::set_inline_descriptions(!self.show_status);
        if ui::current_about().is_empty() {
            self.reset_about();
        }
        self.draw_content(ui, state, v2(rail + nav + 1.0, 0.0), v2(content - 1.0, size.y));
        if self.show_status {
            self.draw_status(ui, state, v2(rail + nav + content + 1.0, 0.0), v2(size.x - rail - nav - content - 1.0, size.y));
        }
        unsafe { sys::igEnd() };
    }

    fn draw_rail(&mut self, width: f32, height: f32) {
        let scale = ui::scale();
        let p = ui::p();
        let draw = Draw::window();
        let origin: V2 = unsafe { sys::igGetWindowPos().into() };

        // brand mark: a bold letter (or icon) with the brand dot at its baseline
        let mark = self.logo.clone().unwrap_or_else(|| self.title.chars().next().map(|c| c.to_uppercase().collect()).unwrap_or_default());
        let mark_size = fonts::brand().calc_text_size(&mark);
        let dot = 2.6 * scale;
        let mark_pos = origin + v2((width - mark_size.x - dot * 2.4) * 0.5, 29.0 * scale - mark_size.y * 0.5);
        draw.text_font(fonts::brand(), mark_pos, theme::u32(p.text, 1.0), &mark);
        draw.circle_filled(mark_pos + v2(mark_size.x + dot * 1.3, mark_size.y * 0.78), dot, theme::u32(p.brand, 1.0));

        // Icons: top-aligned pages from the top, pinned pages from the bottom. The tile glides beneath them.
        let tile = 40.0 * scale;
        let pitch = 52.0 * scale;
        let top = 99.0 * scale;
        let bottom = height - 40.0 * scale;
        let mut tops = 0;
        let mut bottoms = self.pages.iter().filter(|page| page.pin_bottom).count();
        let mut selected_y = 0.0;
        let mut clicked = None;
        draw.channels_split(2);
        draw.channels_set_current(1);
        for (i, page) in self.pages.iter().enumerate() {
            let cy = if page.pin_bottom {
                bottoms -= 1;
                bottom - bottoms as f32 * pitch
            } else {
                tops += 1;
                top + (tops - 1) as f32 * pitch
            };
            let min = v2((width - tile) * 0.5, cy - tile * 0.5);
            ui::set_cursor_pos(min);
            ui::push_id_int(i as i32);
            if ui::invisible_button("##rail", V2::splat(tile)) {
                clicked = Some(i);
            }
            let hovered = ui::is_item_hovered();
            if hovered {
                ui::hand_cursor();
            }
            let hover = anim::toggle(ui::get_id(c"##hover"), hovered && i != self.selected, 18.0);
            let active = anim::toggle(ui::get_id(c"##active"), i == self.selected, 16.0);
            ui::pop_id();
            rail_tooltip(&page.title);

            let center = origin + min + V2::splat(tile * 0.5);
            let color = crate::palette::lerp(crate::palette::lerp(p.text_muted, p.text, hover), p.on_accent, active);
            let glyph = fonts::icon_large().calc_text_size(&page.icon);
            draw.text_font(fonts::icon_large(), center - glyph * 0.5, theme::u32(color, 1.0), &page.icon);
            if page.badge {
                draw.circle_filled(center + v2(tile * 0.3, -tile * 0.3), 3.0 * scale, theme::u32(p.brand, 1.0));
            }
            if i == self.selected {
                selected_y = min.y;
            }
        }
        draw.channels_set_current(0);
        if !self.pages.is_empty() {
            let y = anim::to(ui::get_id(c"##railtile"), selected_y, 18.0, None);
            let min = origin + v2((width - tile) * 0.5, y);
            draw.rect_filled(min, min + V2::splat(tile), theme::u32(p.accent, 1.0), 9.0 * scale);
        }
        draw.channels_merge();
        if let Some(index) = clicked {
            self.set_selected(index);
        }
    }

    fn draw_nav(&mut self, x: f32, width: f32, height: f32) {
        let scale = ui::scale();
        let p = ui::p();
        let draw = Draw::window();
        let origin = V2::from(unsafe { sys::igGetWindowPos() }) + v2(x, 0.0);
        let t = anim::ease_out_cubic(self.nav_progress);
        let slide = (1.0 - t) * 6.0 * scale;
        let pad = pad();
        let selected = self.selected;

        if let Some(page) = self.pages.get_mut(selected) {
            draw.text_font(fonts::title(), origin + v2(pad + slide, title_y()), theme::u32(p.text, t), &page.title);
            if let Some(caption) = &page.caption {
                draw.text_font(
                    fonts::small(),
                    origin + v2(pad + slide, 59.0 * scale),
                    theme::u32(p.text_faint, t),
                    &caption.to_uppercase(),
                );
            }

            // Tabs. The selected one is in full text color on a soft pill that glides beneath the labels.
            let mut y = 102.0 * scale;
            let row = 29.0 * scale;
            let mut selected_y = y;
            let mut clicked = None;
            draw.channels_split(2);
            draw.channels_set_current(1);
            for (i, tab) in page.tabs.iter().enumerate() {
                ui::set_cursor_pos(v2(x + pad * 0.5, y));
                ui::push_id_int((selected * 1000 + i) as i32);
                if ui::invisible_button("##tab", v2(width - pad, row)) {
                    clicked = Some(i);
                }
                let hovered = ui::is_item_hovered();
                if hovered {
                    ui::hand_cursor();
                }
                let hover = anim::toggle(ui::get_id(c"##hover"), hovered, 18.0);
                let active = anim::toggle(ui::get_id(c"##active"), i == page.selected_tab, 16.0);
                ui::pop_id();
                let color = crate::palette::lerp(p.text_muted, p.text, hover.max(active));
                let text_y = y + (row - ui::text_line_height()) * 0.5;
                draw.text(origin + v2(pad + slide, text_y), theme::u32(color, t), &tab.title);
                if i == page.selected_tab {
                    selected_y = y;
                }
                y += row;
            }
            draw.channels_set_current(0);
            if !page.tabs.is_empty() {
                // Keyed per page so switching pages snaps the pill instead of gliding across pages.
                let pill_y = anim::to(ui::get_id_str(&format!("##tabpill{selected}")), selected_y, 18.0, None);
                let min = origin + v2(pad * 0.5 + slide, pill_y);
                draw.rect_filled(min, min + v2(width - pad, row), theme::u32(p.field, t), 6.0 * scale);
            }
            draw.channels_merge();
            if let Some(index) = clicked {
                page.set_selected_tab(index);
            }
        }

        // footer: brand name, subtitle and version on one baseline, level with the bottom rail icon
        let title_top = height - 40.0 * scale - fonts::title().px() * 0.5;
        let footer = origin + v2(pad, title_top);
        draw.text_font(fonts::title(), footer, theme::u32(p.text, 1.0), &self.title);
        let mut cursor = footer.x + fonts::title().calc_text_size(&self.title).x + 8.0 * scale;
        let small_y = footer.y + fonts::title().px() - fonts::small().px() - 1.0 * scale;
        if let Some(subtitle) = &self.subtitle {
            draw.text_font(fonts::small(), v2(cursor, small_y), theme::u32(p.text_muted, 1.0), subtitle);
            cursor += fonts::small().calc_text_size(subtitle).x + 6.0 * scale;
        }
        if let Some(version) = &self.version {
            draw.text_font(fonts::small_semibold(), v2(cursor, small_y), theme::u32(p.text, 1.0), version);
        }
    }

    fn draw_content(&mut self, ui: &Ui, state: &mut S, position: V2, size: V2) {
        let scale = ui::scale();
        let p = ui::p();
        let draw = Draw::window();
        let origin = V2::from(unsafe { sys::igGetWindowPos() }) + position;
        let t = anim::ease_out_cubic(self.page_progress);
        let pad = pad();
        let selected = self.selected;
        let Some(page) = self.pages.get_mut(selected) else { return };
        let tab_index = page.selected_tab;
        if let Some(tab) = page.tabs.get(tab_index) {
            draw.text_font(fonts::title(), origin + v2(pad + (1.0 - t) * 6.0 * scale, title_y()), theme::u32(p.text, t), &tab.title);
        }

        // Body: fades and rises in after a switch. Each tab keeps its own scroll position.
        ui::set_cursor_pos(position + v2(0.0, body_top()));
        ui::push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, v2(pad, 6.0 * scale));
        ui::push_style_var(sys::ImGuiStyleVar_Alpha, ui::style().Alpha * t);
        let mut body_flags = sys::ImGuiWindowFlags_NoBackground;
        if !page.scroll {
            body_flags |= sys::ImGuiWindowFlags_NoScrollbar | sys::ImGuiWindowFlags_NoScrollWithMouse;
        }
        let id = cstring(&format!("##content{selected}.{tab_index}"));
        unsafe {
            sys::igBeginChild_Str(
                id.as_ptr(),
                v2(size.x, size.y - body_top() - 4.0 * scale).into(),
                sys::ImGuiChildFlags_AlwaysUseWindowPadding as _,
                body_flags as _,
            );
        }
        ui::pop_style_var(2);
        ui::push_style_var(sys::ImGuiStyleVar_Alpha, ui::style().Alpha * t);
        if t < 1.0 {
            ui::set_cursor_pos_y(ui::cursor_pos_y() + (1.0 - t) * 10.0 * scale);
        }
        if let Some(tab) = page.tabs.get_mut(tab_index) {
            (tab.draw)(ui, state);
        }
        if page.scroll {
            ui::dummy(v2(0.0, 6.0 * scale));
        }
        ui::pop_style_var(1);
        unsafe { sys::igEndChild() };
    }

    fn draw_status(&mut self, ui: &Ui, state: &mut S, position: V2, size: V2) {
        let scale = ui::scale();
        let p = ui::p();
        let draw = Draw::window();
        let origin = V2::from(unsafe { sys::igGetWindowPos() }) + position;
        let pad = pad();
        draw.text_font(fonts::title(), origin + v2(pad, title_y()), theme::u32(p.text, 1.0), &self.status_title);

        // The ABOUT hint for the hovered control sits on the bottom edge, as tall as its text; the live readouts fill the
        // space above it.
        let about = ui::current_about();
        let title = ui::current_about_title();
        let wrap = size.x - pad * 2.0;
        let caption_gap = 8.0 * scale;
        let title_gap = 3.0 * scale;
        let mut about_top = size.y;
        if !about.is_empty() {
            let mut height = fonts::small().px() + caption_gap + fonts::small().calc_text_size_wrapped(&about, wrap).y;
            if !title.is_empty() {
                height += fonts::medium().calc_text_size_wrapped(&title, wrap).y + title_gap;
            }
            about_top = size.y - about_bottom() - height;
        }

        let readouts_bottom = if about.is_empty() { size.y - 4.0 * scale } else { about_top - 16.0 * scale };
        ui::set_cursor_pos(position + v2(0.0, body_top()));
        ui::push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, v2(pad, 6.0 * scale));
        unsafe {
            sys::igBeginChild_Str(
                c"##status".as_ptr(),
                v2(size.x, (readouts_bottom - body_top()).max(1.0)).into(),
                sys::ImGuiChildFlags_AlwaysUseWindowPadding as _,
                sys::ImGuiWindowFlags_NoBackground as _,
            );
        }
        ui::pop_style_var(1);
        let page_status = self.pages.get_mut(self.selected).and_then(|page| page.status.as_mut());
        if let Some(status) = page_status {
            status(ui, state);
        } else if let Some(status) = self.status.as_mut() {
            status(ui, state);
        }
        unsafe { sys::igEndChild() };

        if about.is_empty() {
            return;
        }
        // A new description fades in, as content does after a tab switch.
        if self.about_shown.0 != title || self.about_shown.1 != about {
            self.about_shown = (title.clone(), about.clone());
            self.about_progress = 0.0;
        }
        self.about_progress = (self.about_progress + anim::delta() / 0.2).min(1.0);
        ui::set_cursor_pos(position + v2(pad, about_top));
        ui::begin_group();
        ui::push_style_var_v2(sys::ImGuiStyleVar_ItemSpacing, V2::ZERO);
        unsafe { sys::igPushTextWrapPos(sys::igGetCursorPosX() + wrap) };
        fonts::push(fonts::small());
        ui::text(ui, &self.about_title.to_uppercase(), p.text_faint);
        fonts::pop();
        ui::dummy(v2(0.0, caption_gap));
        ui::push_style_var(sys::ImGuiStyleVar_Alpha, ui::style().Alpha * anim::ease_out_cubic(self.about_progress));
        if !title.is_empty() {
            fonts::push(fonts::medium());
            ui::text(ui, &title, p.text);
            fonts::pop();
            ui::dummy(v2(0.0, title_gap));
        }
        fonts::push(fonts::small());
        ui::text(ui, &about, p.text_muted);
        fonts::pop();
        ui::pop_style_var(2);
        unsafe { sys::igPopTextWrapPos() };
        ui::end_group();
    }
}

// ui::tooltip without a &Ui (the rail draws outside any caller-provided closure).
fn rail_tooltip(text: &str) {
    let id = ui::item_id() ^ 0x7007;
    if !unsafe { sys::igIsItemHovered(sys::ImGuiHoveredFlags_ForTooltip as _) } {
        anim::set(id, 0.0);
        return;
    }
    let alpha = anim::to(id, 1.0, 16.0, Some(0.0));
    ui::push_style_var(sys::ImGuiStyleVar_Alpha, ui::style().Alpha * alpha);
    ui::push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, ui::s2(10.0, 7.0));
    if unsafe { sys::igBeginTooltip() } {
        ui::text_unformatted(text);
        unsafe { sys::igEndTooltip() };
    }
    ui::pop_style_var(2);
}
