//! BlazeImGui for Rust: the same menu as the C# library (`src/BlazeImGui`) and the C++ port (`cpp/BlazeImGui`), on
//! [`dear-imgui-rs`](https://crates.io/crates/dear-imgui-rs) (Dear ImGui 1.92+).
//!
//! ```ignore
//! use blaze_imgui::{icons, ui, Overlay, OverlayOptions, Tone};
//!
//! struct App { enabled: bool }
//!
//! let mut overlay = Overlay::<App>::new(OverlayOptions { title: "mytool".into(), ..Default::default() });
//! overlay.menu.add_page("Visuals", icons::EYE, Some("Overlay")).add_tab(
//!     "General",
//!     |ui, app| { ui::toggle(ui, "Enabled", &mut app.enabled, Some("Shown under ABOUT while hovered.")); },
//!     None,
//! );
//! overlay.menu.set_status(|ui, app| {
//!     ui::caption(ui, "Session");
//!     ui::readout(ui, "Overlay", if app.enabled { "on" } else { "off" }, Tone::Success);
//! });
//! overlay.setup(&mut imgui);            // once, after the context exists, before the first frame
//! // every frame, inside the frame:
//! overlay.render(ui, &mut app);         // theme, the menu with its fade, toasts
//! ```
//!
//! Colors come from [`theme::colors`] tokens only, so theme switches and fades apply everywhere. Render thread only,
//! except [`notify::show`], which any thread may call.

pub mod anim;
pub mod fonts;
pub mod icons;
pub mod input;
pub mod math;
pub mod menu;
pub mod notify;
pub mod overlay;
pub mod palette;
pub mod theme;
pub mod ui;

pub use dear_imgui_rs;
pub use fonts::{Font, FontWeight};
pub use menu::{Menu, MenuPage, MenuTab};
pub use notify::ToastCorner;
pub use overlay::{Overlay, OverlayOptions};
pub use palette::{hex, Color, Palette};
pub use theme::ThemeMode;
pub use ui::{ButtonKind, Tone};
