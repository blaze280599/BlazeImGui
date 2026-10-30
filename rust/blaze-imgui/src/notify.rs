//! Toast notifications. [`show`] may be called from any thread; [`draw`] runs on the render thread.

use std::sync::Mutex;

use dear_imgui_rs::{sys, Ui};

use crate::anim;
use crate::fonts;
use crate::icons;
use crate::math::v2;
use crate::ui::{self, Tone};

/// The corner toasts stack from.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum ToastCorner {
    #[default]
    TopRight,
    BottomRight,
    TopLeft,
    BottomLeft,
}

struct Toast {
    id: u32,
    title: String,
    message: Option<String>,
    tone: Tone,
    icon: &'static str,
    seconds: f32,
    age: f32,
    height: f32,
}

struct Queue {
    toasts: Vec<Toast>,
    next_id: u32,
    corner: ToastCorner,
}

static QUEUE: Mutex<Queue> = Mutex::new(Queue { toasts: Vec::new(), next_id: 0, corner: ToastCorner::TopRight });

pub fn set_corner(corner: ToastCorner) {
    QUEUE.lock().unwrap_or_else(|e| e.into_inner()).corner = corner;
}

/// Queues a toast. Thread-safe. The icon matches the tone.
pub fn show(title: &str, message: Option<&str>, tone: Tone, seconds: f32) {
    let icon = match tone {
        Tone::Success => icons::CIRCLE_CHECK,
        Tone::Warning => icons::TRIANGLE_ALERT,
        Tone::Danger => icons::CIRCLE_X,
        _ => icons::INFO,
    };
    let mut queue = QUEUE.lock().unwrap_or_else(|e| e.into_inner());
    let id = queue.next_id;
    queue.next_id += 1;
    queue.toasts.push(Toast { id, title: title.into(), message: message.map(Into::into), tone, icon, seconds, age: 0.0, height: 0.0 });
    if queue.toasts.len() > 6 {
        queue.toasts.remove(0);
    }
}

/// Draws and ages the toasts (`Overlay::render` calls it), menu open or not.
pub fn draw(ui: &Ui) {
    let mut queue = QUEUE.lock().unwrap_or_else(|e| e.into_inner());
    queue.toasts.retain(|t| t.age <= t.seconds + 0.4);
    if queue.toasts.is_empty() {
        return;
    }
    let corner = queue.corner;
    let scale = ui::scale();
    let p = ui::p();
    let (work_pos, work_size) = unsafe {
        let viewport = &*sys::igGetMainViewport();
        (viewport.WorkPos, viewport.WorkSize)
    };
    let margin = 20.0 * scale;
    let gap = 10.0 * scale;
    let width = 320.0 * scale;
    let mut offset = 0.0;
    for toast in queue.toasts.iter_mut().rev() {
        toast.age += anim::delta();
        let appear = anim::ease_out_cubic(toast.age / 0.28);
        let leave = anim::ease_out_cubic((toast.seconds + 0.4 - toast.age) / 0.4);
        let alpha = appear.min(leave);
        let stack = anim::to(0xB1A2_7000 + toast.id, offset, 14.0, Some(offset));
        let slide = (1.0 - alpha) * 28.0 * scale;
        let right = matches!(corner, ToastCorner::TopRight | ToastCorner::BottomRight);
        let top = matches!(corner, ToastCorner::TopRight | ToastCorner::TopLeft);
        let x = if right { work_pos.x + work_size.x - margin + slide } else { work_pos.x + margin - slide };
        let y = if top { work_pos.y + margin + stack } else { work_pos.y + work_size.y - margin - stack };

        let name = ui::cstring(&format!("##toast{}", toast.id));
        unsafe {
            sys::igSetNextWindowPos(
                v2(x, y).into(),
                sys::ImGuiCond_Always as _,
                v2(if right { 1.0 } else { 0.0 }, if top { 0.0 } else { 1.0 }).into(),
            );
            sys::igSetNextWindowSize(v2(width, 0.0).into(), 0);
        }
        ui::push_style_var(sys::ImGuiStyleVar_Alpha, alpha);
        ui::push_style_var_v2(sys::ImGuiStyleVar_WindowPadding, v2(14.0, 12.0) * scale);
        ui::push_style_var(sys::ImGuiStyleVar_WindowRounding, 6.0 * scale);
        ui::push_style_color(sys::ImGuiCol_WindowBg, p.elevated);
        let flags = sys::ImGuiWindowFlags_NoDecoration
            | sys::ImGuiWindowFlags_NoInputs
            | sys::ImGuiWindowFlags_NoNav
            | sys::ImGuiWindowFlags_NoFocusOnAppearing
            | sys::ImGuiWindowFlags_NoSavedSettings
            | sys::ImGuiWindowFlags_NoMove
            | sys::ImGuiWindowFlags_AlwaysAutoResize;
        unsafe { sys::igBegin(name.as_ptr(), std::ptr::null_mut(), flags as _) };
        ui::text_tone(ui, toast.icon, toast.tone);
        ui::same_line(0.0, 10.0 * scale);
        ui::begin_group();
        fonts::push(fonts::semibold());
        ui::text_unformatted(&toast.title);
        fonts::pop();
        if let Some(message) = &toast.message {
            ui::push_style_color(sys::ImGuiCol_Text, p.text_muted);
            unsafe { sys::igPushTextWrapPos(sys::igGetCursorPosX() + width - 60.0 * scale) };
            ui::text_unformatted(message);
            unsafe { sys::igPopTextWrapPos() };
            ui::pop_style_color(1);
        }
        ui::end_group();
        toast.height = unsafe { sys::igGetWindowHeight() };
        unsafe { sys::igEnd() };
        ui::pop_style_color(1);
        ui::pop_style_var(3);
        offset += (toast.height + gap) * (alpha * 1.5).min(1.0);
    }
}
