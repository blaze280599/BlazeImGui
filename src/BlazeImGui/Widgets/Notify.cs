using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>
/// Toast notifications in a screen corner (top-right by default, clear of the Windows taskbar). They slide and fade in, stack, and fade out on their own.
/// Safe to call from any thread: <c>Notify.Show("Saved", "Settings written to disk", Tone.Success);</c>
/// Toasts show even while the menu is hidden.
/// </summary>
public static class Notify
{
    private sealed class Toast
    {
        public required int Id;
        public required string Title;
        public required string? Message;
        public required Tone Tone;
        public required string Icon;
        public required float Seconds;
        public float Age;
        public float Height;
    }

    private static readonly List<Toast> Toasts = new();
    private static readonly object Gate = new();
    private static int _nextId;

    /// <summary>The corner toasts stack from.</summary>
    public static ToastCorner Corner { get; set; } = ToastCorner.TopRight;

    public static void Show(string title, string? message = null, Tone tone = Tone.Accent, float seconds = 3.5f, string? icon = null)
    {
        icon ??= tone switch
        {
            Tone.Success => Icons.CircleCheck,
            Tone.Warning => Icons.TriangleAlert,
            Tone.Danger => Icons.CircleX,
            _ => Icons.Info,
        };
        lock (Gate)
        {
            Toasts.Add(new Toast { Id = _nextId++, Title = title, Message = message, Tone = tone, Icon = icon, Seconds = seconds });
            if (Toasts.Count > 6) Toasts.RemoveAt(0);
        }
    }

    /// <summary>Draws and ages the toasts. Called every frame by <see cref="BlazeOverlay"/>.</summary>
    public static void Draw()
    {
        Toast[] toasts;
        lock (Gate)
        {
            Toasts.RemoveAll(t => t.Age > t.Seconds + .4f);
            toasts = Toasts.ToArray();
        }
        if (toasts.Length == 0) return;

        var scale = Ui.Scale;
        var viewport = ImGui.GetMainViewport();
        var margin = 20f * scale;
        var gap = 10f * scale;
        var width = 320f * scale;
        var offset = 0f;
        var p = Theme.Colors;
        for (var i = toasts.Length - 1; i >= 0; i--)
        {
            var toast = toasts[i];
            toast.Age += Anim.Delta;
            var appear = Anim.EaseOutCubic(toast.Age / .28f);
            var leave = Anim.EaseOutCubic((toast.Seconds + .4f - toast.Age) / .4f);
            var alpha = MathF.Min(appear, leave);
            var stack = Anim.To(0xB1A2_7000u + (uint)toast.Id, offset, 14f, offset);
            var slide = (1f - alpha) * 28f * scale;
            var right = Corner is ToastCorner.TopRight or ToastCorner.BottomRight;
            var top = Corner is ToastCorner.TopRight or ToastCorner.TopLeft;
            var x = right ? viewport.WorkPos.X + viewport.WorkSize.X - margin + slide : viewport.WorkPos.X + margin - slide;
            var y = top ? viewport.WorkPos.Y + margin + stack : viewport.WorkPos.Y + viewport.WorkSize.Y - margin - stack;
            ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always, new Vector2(right ? 1 : 0, top ? 0 : 1));
            ImGui.SetNextWindowSize(new Vector2(width, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, alpha);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * scale);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10 * scale);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, p.Elevated);
            ImGui.Begin($"##toast{toast.Id}", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize);
            Ui.Text(toast.Icon, toast.Tone);
            ImGui.SameLine(0, 10 * scale);
            ImGui.BeginGroup();
            ImGui.PushFont(Fonts.SemiBold);
            ImGui.TextUnformatted(toast.Title);
            ImGui.PopFont();
            if (toast.Message is not null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, p.TextMuted);
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - 60 * scale);
                ImGui.TextUnformatted(toast.Message);
                ImGui.PopTextWrapPos();
                ImGui.PopStyleColor();
            }
            ImGui.EndGroup();
            toast.Height = ImGui.GetWindowHeight();
            ImGui.End();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(3);
            offset += (toast.Height + gap) * MathF.Min(1f, alpha * 1.5f);
        }
    }
}

public enum ToastCorner { TopRight, BottomRight, TopLeft, BottomLeft }
