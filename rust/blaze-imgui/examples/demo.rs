//! BlazeImGui Rust demo: a winit + OpenGL (glutin/glow) window with the menu and a few pages of widgets.
//!
//!   cargo run --example demo -- [--theme dark] [--page "Controls/Buttons"] [--screenshot out.bmp]
//!
//! `--screenshot` renders off-screen for a few frames, saves the back buffer as a BMP and exits. Insert toggles the menu.

use std::error::Error;
use std::num::NonZeroU32;
use std::sync::Arc;

use blaze_imgui::dear_imgui_rs::Context;
use blaze_imgui::input::{self, vk};
use blaze_imgui::{hex, icons, notify, theme, ui, ButtonKind, Color, Overlay, OverlayOptions, ThemeMode, Tone};
use dear_imgui_glow::{glow, glow::HasContext, GlowRenderer};
use dear_imgui_winit::{HiDpiMode, WinitPlatform};
use glutin::config::ConfigTemplateBuilder;
use glutin::context::{ContextAttributesBuilder, PossiblyCurrentContext};
use glutin::display::GetGlDisplay;
use glutin::prelude::*;
use glutin::surface::{Surface, SwapInterval, WindowSurface};
use glutin_winit::{DisplayBuilder, GlWindow};
use raw_window_handle::HasWindowHandle;
use winit::application::ApplicationHandler;
use winit::dpi::{LogicalSize, PhysicalPosition};
use winit::event::WindowEvent;
use winit::event_loop::{ActiveEventLoop, EventLoop};
use winit::window::{Window, WindowId};

/// The demo's settings: the menu's closures receive `&mut App`.
struct App {
    esp: bool,
    names: bool,
    health: bool,
    unfocused: bool,
    check: bool,
    saved: bool,
    distance: f32,
    smoothing: f32,
    label_scale: f32,
    fov: i32,
    mode: usize,
    segment: usize,
    theme: usize,
    key: i32,
    count: i32,
    name: String,
    color: Color,
    series: Vec<f32>,
}

struct Args {
    dark: bool,
    page: Option<String>,
    screenshot: Option<String>,
}

fn build_overlay(dark: bool) -> Overlay<App> {
    let mut overlay = Overlay::<App>::new(OverlayOptions {
        title: "blaze".into(),
        subtitle: Some("Rust".into()),
        version: Some("v1.0.0".into()),
        theme: if dark { ThemeMode::Dark } else { ThemeMode::Light },
        ..Default::default()
    });
    let menu = &mut overlay.menu;

    menu.add_page("Visuals", icons::EYE, Some("Overlay"))
        .add_tab(
            "General",
            |ui, app| {
                ui::toggle(ui, "Enable visuals", &mut app.esp, Some("Draw players and objects in the world."));
                let disabled = ui.begin_disabled_with_cond(!app.esp);
                ui::slider(
                    ui,
                    "Max distance",
                    &mut app.distance,
                    10.0,
                    500.0,
                    |v| format!("{v:.0}"),
                    Some("Players further away than this are not drawn. 150 - 300 suits most maps."),
                );
                ui::slider_int(
                    ui,
                    "Field of view",
                    &mut app.fov,
                    30,
                    180,
                    Some("How wide the drawn area is around the crosshair, in degrees."),
                );
                ui::toggle(ui, "Names", &mut app.names, Some("A name tag above each player."));
                ui::toggle(ui, "Health bars", &mut app.health, Some("A thin bar beside each player, colored by health."));
                disabled.end();
                ui::toggle(ui, "Hide when game is unfocused", &mut app.unfocused, Some("Stops drawing while another window has focus."));
            },
            Some("What is drawn over the game. Hover a setting to read about it here."),
        )
        .add_tab(
            "Colors",
            |ui, app| {
                ui::color(ui, "Enemy", &mut app.color, true, Some("Boxes, names and the crosshair for enemies."));
            },
            Some("Colors used by the world drawings."),
        );

    menu.add_page("Controls", icons::CROSSHAIR, Some("Widgets"))
        .add_tab(
            "Inputs",
            |ui, app| {
                ui::combo(ui, "Mode", &mut app.mode, &["Automatic", "Manual", "Disabled"], Some("How the overlay picks its target."));
                ui::slider(
                    ui,
                    "Smoothing",
                    &mut app.smoothing,
                    1.0,
                    10.0,
                    |v| format!("{v:.1}"),
                    Some("Higher values ease movement more."),
                );
                ui::slider(
                    ui,
                    "Label size",
                    &mut app.label_scale,
                    0.75,
                    1.5,
                    |v| format!("{v:.2}x"),
                    Some("Snaps to the two decimals it shows; Left/Right step by 0.01."),
                );
                ui::text_input(ui, "Profile name", &mut app.name, "e.g. Raid night", 64, None);
                ui::number_int(ui, "Count", &mut app.count, 0.2, 0, 99, None);
                ui::row(ui, "Quality", 0.0);
                ui::segmented(ui, "##quality", &mut app.segment, &["Low", "Mid", "High"], 0.0);
            },
            Some("Dropdowns, sliders, fields and choices."),
        )
        .add_tab(
            "Buttons",
            |ui, app| {
                let width = [ui::control_width(ui), 0.0];
                if ui::button(ui, "Save", ButtonKind::Secondary, width) {
                    notify::show("Saved", Some("Settings written to disk"), Tone::Success, 3.5);
                }
                ui.spacing();
                ui::button(ui, "Primary", ButtonKind::Primary, width);
                ui.spacing();
                ui::button(ui, "Ghost", ButtonKind::Ghost, [0.0, 0.0]);
                ui.same_line();
                ui::button(ui, "Delete", ButtonKind::Danger, [0.0, 0.0]);
                ui.spacing();
                ui::checkbox(ui, "Players", &mut app.check);
                ui.same_line_with_spacing(0.0, 18.0);
                ui::checkbox(ui, "NPCs", &mut app.names);
                ui::keybind(ui, "Toggle key", &mut app.key, Some("Click, then press a key. Esc cancels, Backspace clears."));
            },
            Some("Outlined by default; primary is filled with the ink color."),
        );

    let charts = menu.add_page("Charts", icons::CHART_SPLINE, Some("Live data"));
    charts.add_tab(
        "Live",
        |ui, app| {
            if ui::begin_grid(ui, "stats", 2) {
                ui::next_cell(ui);
                ui::stat(ui, "Players", "24", Some("+3"), Tone::Success);
                ui::next_cell(ui);
                ui::stat(ui, "Frame time", "6.9 ms", Some("-0.4"), Tone::Success);
                ui::end_grid(ui);
            }
            ui::section(ui, "Throughput");
            ui::line_chart(ui, "##series", &app.series, 0.0, None, None, Tone::Accent);
            ui::section(ui, "Progress");
            ui::progress(ui, 0.62, Some("Download"), Tone::Accent, 0.0);
            ui::progress(ui, 0.25, Some("Upload"), Tone::Warning, 0.0);
        },
        Some("ui::line_chart with a hover readout."),
    );
    charts.badge = true;

    let settings = menu.add_page("Settings", icons::SETTINGS, Some("Menu"));
    settings.add_tab(
        "General",
        |ui, app| {
            ui::keybind(ui, "Menu key", &mut app.key, Some("The key that shows and hides this menu."));
            ui::row(ui, "Theme", 0.0);
            if ui::segmented(ui, "##theme", &mut app.theme, &["Light", "Dark"], 0.0) {
                theme::set(if app.theme == 1 { ThemeMode::Dark } else { ThemeMode::Light }, true);
            }
            ui.dummy([0.0, 8.0]);
            let width = [ui::control_width(ui), 0.0];
            if ui::button(ui, "Save config", ButtonKind::Secondary, width) {
                app.saved = true;
            }
            ui.spacing();
            if ui::button(ui, "Close menu", ButtonKind::Secondary, width) {
                blaze_imgui::menu::request_close();
            }
        },
        Some("Theme, hotkeys and the config file."),
    );
    settings.pin_bottom = true;
    settings.set_status(|ui, app| {
        ui::caption(ui, "Config");
        ui::readout(ui, "Menu key", &input::key_name(app.key), Tone::Neutral);
        ui::readout(ui, "File", "blaze.ini", Tone::Neutral);
        let (text, tone) = if app.saved { ("saved", Tone::Success) } else { ("not saved", Tone::Warning) };
        ui::readout(ui, "Changes", text, tone);
    });

    menu.set_status(|ui, app| {
        ui::caption(ui, "Session");
        ui::readout(ui, "Overlay", "running", Tone::Success);
        ui::readout(ui, "Frame rate", &format!("{:.0} fps", ui.io().framerate()), Tone::Neutral);
        let (text, tone) = if app.esp { ("drawing", Tone::Success) } else { ("off", Tone::Neutral) };
        ui::readout(ui, "Visuals", text, tone);
        ui::readout(ui, "Distance", &format!("{:.0} yd  (FOV {})", app.distance, app.fov), Tone::Neutral);
        ui::readout(ui, "Theme", if theme::is_dark() { "dark" } else { "light" }, Tone::Neutral);
    });
    overlay
}

struct Gpu {
    window: Arc<Window>,
    surface: Surface<WindowSurface>,
    context: PossiblyCurrentContext,
    gl: glow::Context,
    imgui: Context,
    platform: WinitPlatform,
    renderer: GlowRenderer,
}

struct Demo {
    args: Args,
    overlay: Overlay<App>,
    app: App,
    gpu: Option<Gpu>,
    frame: u32,
}

impl Demo {
    fn create(&mut self, event_loop: &ActiveEventLoop) -> Result<Gpu, Box<dyn Error>> {
        let mut attributes =
            Window::default_attributes().with_title("BlazeImGui Rust demo").with_inner_size(LogicalSize::new(1100.0, 720.0));
        // A screenshot run renders off-screen so nothing flashes on the desktop.
        if self.args.screenshot.is_some() {
            attributes = attributes.with_position(PhysicalPosition::new(-4000, 100)).with_active(false);
        }
        let (window, config) =
            DisplayBuilder::new().with_window_attributes(Some(attributes)).build(event_loop, ConfigTemplateBuilder::new(), |configs| {
                configs.reduce(|a, b| if b.num_samples() < a.num_samples() { b } else { a }).expect("an OpenGL config")
            })?;
        let window = Arc::new(window.ok_or("no window")?);
        let display = config.display();
        let raw = window.window_handle()?.as_raw();
        let context = unsafe { display.create_context(&config, &ContextAttributesBuilder::new().build(Some(raw)))? };
        let surface_attributes = window.build_surface_attributes(Default::default())?;
        let surface = unsafe { display.create_window_surface(&config, &surface_attributes)? };
        let context = context.make_current(&surface)?;
        let _ = surface.set_swap_interval(&context, SwapInterval::Wait(NonZeroU32::MIN));
        let loader = |name: &std::ffi::CStr| display.get_proc_address(name) as *const _;
        let gl = unsafe { glow::Context::from_loader_function_cstr(loader) };

        let mut imgui = Context::create();
        imgui.set_ini_filename(None::<String>)?;
        let mut platform = WinitPlatform::new(&mut imgui)?;
        platform.attach_window(Arc::clone(&window), HiDpiMode::Default, &mut imgui)?;
        // Fonts, theme and style before the renderer builds its font texture.
        self.overlay.setup(&mut imgui);
        if let Some(page) = &self.args.page {
            self.overlay.menu.select(page);
        }
        let renderer = GlowRenderer::new(unsafe { glow::Context::from_loader_function_cstr(loader) }, &mut imgui)?;
        Ok(Gpu { window, surface, context, gl, imgui, platform, renderer })
    }

    fn redraw(&mut self, event_loop: &ActiveEventLoop) -> Result<(), Box<dyn Error>> {
        let Some(gpu) = self.gpu.as_mut() else { return Ok(()) };
        // A slowly moving series for the chart.
        let t = self.frame as f32 * 0.05;
        self.app.series.rotate_left(1);
        if let Some(last) = self.app.series.last_mut() {
            *last = 50.0 + 18.0 * (t * 0.9).sin() + 9.0 * (t * 2.3 + 1.0).sin();
        }

        gpu.platform.prepare_frame(&mut gpu.imgui, &gpu.window)?;
        let ui = gpu.imgui.frame();
        if input::pressed(vk::INSERT) {
            self.overlay.toggle_menu();
        }
        self.overlay.render(ui, &mut self.app);
        gpu.platform.prepare_render(ui, &gpu.window)?;

        let clear = hex("#17121D", 1.0);
        unsafe {
            gpu.gl.clear_color(clear[0], clear[1], clear[2], 1.0);
            gpu.gl.clear(glow::COLOR_BUFFER_BIT);
        }
        let frame = gpu.imgui.render(gpu.renderer.renderer_consumer()?);
        gpu.renderer.render(frame)?;

        self.frame += 1;
        if let (Some(path), 90) = (&self.args.screenshot, self.frame) {
            let size = gpu.window.inner_size();
            save_bmp(&gpu.gl, size.width, size.height, path)?;
            event_loop.exit();
        }
        gpu.surface.swap_buffers(&gpu.context)?;
        gpu.window.request_redraw();
        Ok(())
    }
}

impl ApplicationHandler for Demo {
    fn resumed(&mut self, event_loop: &ActiveEventLoop) {
        if self.gpu.is_none() {
            match self.create(event_loop) {
                Ok(gpu) => {
                    gpu.window.request_redraw();
                    self.gpu = Some(gpu);
                }
                Err(error) => {
                    eprintln!("startup failed: {error}");
                    event_loop.exit();
                }
            }
        }
    }

    fn window_event(&mut self, event_loop: &ActiveEventLoop, id: WindowId, event: WindowEvent) {
        if let Some(gpu) = self.gpu.as_mut() {
            let _ = gpu.platform.handle_window_event(&mut gpu.imgui, id, &event);
            if let WindowEvent::Resized(size) = event {
                if let (Some(w), Some(h)) = (NonZeroU32::new(size.width), NonZeroU32::new(size.height)) {
                    gpu.surface.resize(&gpu.context, w, h);
                }
            }
        }
        match event {
            WindowEvent::CloseRequested => event_loop.exit(),
            WindowEvent::RedrawRequested => {
                if let Err(error) = self.redraw(event_loop) {
                    eprintln!("frame failed: {error}");
                    event_loop.exit();
                }
            }
            _ => {}
        }
    }

    fn exiting(&mut self, _event_loop: &ActiveEventLoop) {
        if let Some(mut gpu) = self.gpu.take() {
            let _ = gpu.renderer.shutdown(&mut gpu.imgui);
            let _ = gpu.platform.shutdown(&mut gpu.imgui);
        }
    }
}

// The back buffer as a 24-bit BMP.
fn save_bmp(gl: &glow::Context, width: u32, height: u32, path: &str) -> std::io::Result<()> {
    let mut rgba = vec![0u8; (width * height * 4) as usize];
    unsafe {
        gl.read_buffer(glow::BACK);
        gl.read_pixels(0, 0, width as i32, height as i32, glow::RGBA, glow::UNSIGNED_BYTE, glow::PixelPackData::Slice(Some(&mut rgba)));
    }
    let stride = ((width * 3 + 3) & !3) as usize;
    let mut pixels = vec![0u8; stride * height as usize];
    // GL rows run bottom-up, as BMP rows do.
    for y in 0..height as usize {
        for x in 0..width as usize {
            let src = (y * width as usize + x) * 4;
            let dst = y * stride + x * 3;
            pixels[dst] = rgba[src + 2];
            pixels[dst + 1] = rgba[src + 1];
            pixels[dst + 2] = rgba[src];
        }
    }
    let mut file = Vec::with_capacity(54 + pixels.len());
    file.extend_from_slice(b"BM");
    file.extend_from_slice(&(54 + pixels.len() as u32).to_le_bytes());
    file.extend_from_slice(&[0; 4]);
    file.extend_from_slice(&54u32.to_le_bytes());
    file.extend_from_slice(&40u32.to_le_bytes());
    file.extend_from_slice(&(width as i32).to_le_bytes());
    file.extend_from_slice(&(height as i32).to_le_bytes());
    file.extend_from_slice(&1u16.to_le_bytes());
    file.extend_from_slice(&24u16.to_le_bytes());
    file.extend_from_slice(&[0; 24]);
    file.extend_from_slice(&pixels);
    std::fs::write(path, file)
}

fn main() -> Result<(), Box<dyn Error>> {
    let mut args = Args { dark: false, page: None, screenshot: None };
    let mut it = std::env::args().skip(1);
    while let Some(arg) = it.next() {
        match arg.as_str() {
            "--theme" => args.dark = it.next().as_deref() == Some("dark"),
            "--page" => args.page = it.next(),
            "--screenshot" => args.screenshot = it.next(),
            _ => {}
        }
    }
    let app = App {
        esp: true,
        names: true,
        health: true,
        unfocused: false,
        check: true,
        saved: true,
        distance: 250.0,
        smoothing: 4.5,
        label_scale: 1.0,
        fov: 90,
        mode: 0,
        segment: 1,
        theme: usize::from(args.dark),
        key: vk::F6,
        count: 12,
        name: String::new(),
        color: hex("#FF6B6B", 1.0),
        series: (0..60).map(|i| 50.0 + 18.0 * (i as f32 * 0.045).sin() + 9.0 * (i as f32 * 0.115 + 1.0).sin()).collect(),
    };
    let overlay = build_overlay(args.dark);
    let mut demo = Demo { args, overlay, app, gpu: None, frame: 0 };
    EventLoop::new()?.run_app(&mut demo)?;
    Ok(())
}
