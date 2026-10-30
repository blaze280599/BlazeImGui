//! Ties the pieces together for a host that owns the ImGui context and backends (a winit app, a hook). The host
//! routes input and handles its own hotkeys (skip them while [`crate::ui::is_capturing_key`] or `io.want_text_input`).

use dear_imgui_rs::{sys, Context, Ui};

use crate::anim;
use crate::fonts;
use crate::input;
use crate::menu::Menu;
use crate::notify;
use crate::palette::Color;
use crate::theme::{self, ThemeMode};
use crate::ui;

pub struct OverlayOptions {
    /// Brand name under the tabs.
    pub title: String,
    /// Small muted word after it ("osu!").
    pub subtitle: Option<String>,
    /// After the subtitle ("v1.0.0").
    pub version: Option<String>,
    /// Rail brand mark; `None`: the first letter of the title.
    pub logo: Option<String>,
    pub start_visible: bool,
    pub theme: ThemeMode,
    /// Default: ink (near-black, near-white in dark).
    pub accent: Option<Color>,
    /// Body px before scale.
    pub font_size: f32,
    pub ui_scale: f32,
    pub menu_size: [f32; 2],
}

impl Default for OverlayOptions {
    fn default() -> Self {
        OverlayOptions {
            title: "Blaze Overlay".into(),
            subtitle: None,
            version: None,
            logo: None,
            start_visible: true,
            theme: ThemeMode::Light,
            accent: None,
            font_size: 15.0,
            ui_scale: 1.0,
            menu_size: [1000.0, 600.0],
        }
    }
}

/// The menu, its fade and the toasts. `S` is your application state, lent to the menu's closures each frame.
pub struct Overlay<S> {
    pub menu: Menu<S>,
    options: OverlayOptions,
    menu_visible: bool,
    menu_alpha: f32,
}

const MENU_ANIMATION_ID: u32 = 0xB1A2_E001;

impl<S> Overlay<S> {
    pub fn new(options: OverlayOptions) -> Self {
        let mut menu = Menu::new();
        menu.title = options.title.clone();
        menu.subtitle = options.subtitle.clone();
        menu.version = options.version.clone();
        menu.logo = options.logo.clone();
        menu.size = options.menu_size;
        Overlay { menu, menu_visible: options.start_visible, menu_alpha: 0.0, options }
    }

    /// Once, after the context exists and before the first frame (and before the renderer builds the font atlas, if
    /// it does so eagerly): fonts, theme, style.
    pub fn setup(&mut self, _context: &mut Context) {
        ui::set_scale(self.options.ui_scale);
        fonts::load(self.options.font_size);
        theme::set(self.options.theme, false);
        if let Some(accent) = self.options.accent {
            theme::set_accent(accent, false);
        }
        unsafe { (*sys::igGetStyle()).FontScaleMain = self.options.ui_scale };
        theme::apply_style(self.options.ui_scale);
    }

    /// Every frame, inside the frame: input, theme, the menu with its fade, toasts.
    pub fn render(&mut self, ui: &Ui, state: &mut S) {
        input::update();
        theme::update();
        if self.menu.take_close_request() {
            self.menu_visible = false;
        }
        self.menu_alpha =
            anim::to(MENU_ANIMATION_ID, if self.menu_visible { 1.0 } else { 0.0 }, if self.menu_visible { 14.0 } else { 20.0 }, Some(0.0));
        if self.menu_alpha > 0.002 {
            ui::push_style_var(sys::ImGuiStyleVar_Alpha, self.menu_alpha);
            self.menu.draw(ui, state, self.menu_alpha, self.menu_visible);
            ui::pop_style_var(1);
            if self.menu.take_close_request() {
                self.menu_visible = false;
            }
        }
        notify::draw(ui);
    }

    pub fn menu_visible(&self) -> bool {
        self.menu_visible
    }

    pub fn set_menu_visible(&mut self, visible: bool) {
        self.menu_visible = visible;
    }

    pub fn toggle_menu(&mut self) {
        self.menu_visible = !self.menu_visible;
    }

    /// The menu's fade, 0 hidden .. 1 shown.
    pub fn menu_alpha(&self) -> f32 {
        self.menu_alpha
    }

    /// Changes the UI scale at runtime (e.g. when the game resolution changes): sizes and fonts.
    pub fn set_scale(&mut self, scale: f32) {
        if (scale - ui::scale()).abs() < 0.001 {
            return;
        }
        ui::set_scale(scale);
        unsafe { (*sys::igGetStyle()).FontScaleMain = scale };
        theme::apply_style(scale);
    }

    pub fn options(&self) -> &OverlayOptions {
        &self.options
    }
}
