using System.Numerics;
using ImGuiNET;

namespace BlazeImGui;

/// <summary>
/// Tiny frame-rate independent animation helpers. Every animated value is keyed by an ImGui ID, so a widget
/// can call <c>Anim.To(id, target)</c> every frame and get a value that eases toward the target.
/// </summary>
public static class Anim
{
    private static readonly Dictionary<uint, float> Values = new();

    /// <summary>Seconds since the last frame, clamped so a stall never makes animations jump.</summary>
    public static float Delta => Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);

    /// <summary>
    /// Moves the stored value for <paramref name="id"/> toward <paramref name="target"/> with exponential smoothing.
    /// <paramref name="speed"/> is roughly "1 / time constant": 10 is relaxed, 16 is snappy, 30 is near instant.
    /// The first call returns <paramref name="initial"/> (or the target when omitted).
    /// </summary>
    public static float To(uint id, float target, float speed = 14f, float? initial = null)
    {
        if (!Values.TryGetValue(id, out var value)) value = initial ?? target;
        value += (target - value) * (1f - MathF.Exp(-speed * Delta));
        if (MathF.Abs(target - value) < 0.0005f) value = target;
        Values[id] = value;
        return value;
    }

    /// <summary>Same as <see cref="To(uint,float,float,float?)"/> with a string key scoped to the current ImGui ID stack.</summary>
    public static float To(string key, float target, float speed = 14f, float? initial = null)
        => To(ImGui.GetID(key), target, speed, initial);

    /// <summary>Animates toward 1 while <paramref name="on"/> is true and toward 0 otherwise.</summary>
    public static float Toggle(uint id, bool on, float speed = 14f) => To(id, on ? 1f : 0f, speed);

    /// <summary>Overwrites a stored value, e.g. to restart a transition from 0.</summary>
    public static void Set(uint id, float value) => Values[id] = value;

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => a + (b - a) * t;

    public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - Math.Clamp(t, 0f, 1f), 3f);
    public static float EaseInOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < .5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
