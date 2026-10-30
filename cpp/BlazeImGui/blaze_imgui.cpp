#ifndef IMGUI_DEFINE_MATH_OPERATORS
#define IMGUI_DEFINE_MATH_OPERATORS
#endif
#include "blaze_imgui.h"

#include <algorithm>
#include <cctype>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <unordered_map>
#include <vector>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#endif

#include "blaze_font_data.h"

namespace
{
    float S(float value) { return value * Blaze::Ui::Scale; }
    const Blaze::Palette& P() { return Blaze::Theme::Colors(); }
    ImU32 Col(const ImVec4& color, float alpha = 1.0f) { return Blaze::Theme::U32(color, alpha); }

    std::string Format(const char* format, float value)
    {
        char text[64];
        snprintf(text, sizeof(text), format, value);
        return text;
    }
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// palette
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace Blaze
{
    ImVec4 Palette::Hex(const char* hex, float alpha)
    {
        if (*hex == '#')
            ++hex;

        const auto channel = [&](int i) {
            char pair[3] = { hex[i], hex[i + 1], 0 };
            return static_cast<float>(std::strtol(pair, nullptr, 16)) / 255.0f;
        };
        return ImVec4(channel(0), channel(2), channel(4), std::strlen(hex) >= 8 ? channel(6) : alpha);
    }

    ImVec4 Palette::Shade(const ImVec4& c, float amount)
    {
        const ImVec4 target = amount >= 0.0f ? ImVec4(1, 1, 1, 1) : ImVec4(0, 0, 0, 1);
        ImVec4 mixed = c + (target - c) * std::fabs(amount);
        mixed.w = c.w;
        return mixed;
    }

    ImVec4 Palette::Alpha(const ImVec4& c, float alpha) { return ImVec4(c.x, c.y, c.z, alpha); }

    Palette Palette::WithAccent(const ImVec4& accent, bool lightTheme) const
    {
        Palette copy = *this;
        copy.Accent = accent;
        copy.AccentHover = lightTheme ? Shade(accent, -0.08f) : Shade(accent, 0.08f);
        return copy;
    }

    Palette Palette::Lerp(const Palette& a, const Palette& b, float t)
    {
        const auto L = [t](const ImVec4& x, const ImVec4& y) { return Anim::Lerp(x, y, t); };
        Palette p;
        p.Background = L(a.Background, b.Background); p.Sidebar = L(a.Sidebar, b.Sidebar);
        p.Surface = L(a.Surface, b.Surface); p.Elevated = L(a.Elevated, b.Elevated);
        p.Field = L(a.Field, b.Field); p.FieldHover = L(a.FieldHover, b.FieldHover); p.FieldActive = L(a.FieldActive, b.FieldActive);
        p.Border = L(a.Border, b.Border); p.Raised = L(a.Raised, b.Raised);
        p.Text = L(a.Text, b.Text); p.TextMuted = L(a.TextMuted, b.TextMuted); p.TextFaint = L(a.TextFaint, b.TextFaint);
        p.Accent = L(a.Accent, b.Accent); p.AccentHover = L(a.AccentHover, b.AccentHover); p.OnAccent = L(a.OnAccent, b.OnAccent);
        p.Knob = L(a.Knob, b.Knob); p.Brand = L(a.Brand, b.Brand);
        p.Success = L(a.Success, b.Success); p.Warning = L(a.Warning, b.Warning); p.Danger = L(a.Danger, b.Danger);
        return p;
    }

    Palette Palette::DefaultDark()
    {
        Palette p;
        p.Background = Hex("#17171B");
        p.Sidebar = Hex("#17171B");
        p.Surface = Hex("#1D1D22");
        p.Elevated = Hex("#202026");
        p.Field = Hex("#2A2A31");
        p.FieldHover = Hex("#313139");
        p.FieldActive = Hex("#393941");
        p.Raised = Hex("#33333B");
        p.Border = Hex("#FFFFFF", 0.08f);
        p.Text = Hex("#E7E7EB");
        p.TextMuted = Hex("#8C8C96");
        p.TextFaint = Hex("#56565F");
        p.Accent = Hex("#E7E7EB");
        p.AccentHover = Hex("#FFFFFF");
        p.OnAccent = Hex("#17171B");
        p.Knob = Hex("#F2F2F5");
        p.Brand = Hex("#FF5C9D");
        p.Success = Hex("#3DD68C");
        p.Warning = Hex("#F5A524");
        p.Danger = Hex("#FF6B6B");
        return p;
    }

    Palette Palette::DefaultLight()
    {
        Palette p;
        p.Background = Hex("#ECECF0");
        p.Sidebar = Hex("#ECECF0");
        p.Surface = Hex("#F3F3F6");
        p.Elevated = Hex("#F7F7F9");
        p.Field = Hex("#E1E1E6");
        p.FieldHover = Hex("#D9D9DF");
        p.FieldActive = Hex("#CFCFD6");
        p.Raised = Hex("#F9F9FB");
        p.Border = Hex("#0B0B12", 0.10f);
        p.Text = Hex("#2B2B30");
        p.TextMuted = Hex("#8E8E98");
        p.TextFaint = Hex("#B6B6BF");
        p.Accent = Hex("#2B2B30");
        p.AccentHover = Hex("#3B3B41");
        p.OnAccent = Hex("#F3F3F6");
        p.Knob = Hex("#F9F9FB");
        p.Brand = Hex("#FF5C9D");
        p.Success = Hex("#2EA44F");
        p.Warning = Hex("#E8820C");
        p.Danger = Hex("#E5484D");
        return p;
    }
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// animation
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    std::unordered_map<ImGuiID, float>& AnimValues()
    {
        static std::unordered_map<ImGuiID, float> values;
        return values;
    }
}

float Blaze::Anim::Delta()
{
    return std::clamp(ImGui::GetIO().DeltaTime, 0.0f, 0.1f);
}

float Blaze::Anim::To(ImGuiID id, float target, float speed, std::optional<float> initial)
{
    auto& values = AnimValues();
    auto found = values.find(id);
    float value = found != values.end() ? found->second : initial.value_or(target);
    value += (target - value) * (1.0f - std::exp(-speed * Delta()));

    if (std::fabs(target - value) < 0.0005f)
        value = target;

    values[id] = value;
    return value;
}

float Blaze::Anim::To(const char* key, float target, float speed, std::optional<float> initial)
{
    return To(ImGui::GetID(key), target, speed, initial);
}

float Blaze::Anim::Toggle(ImGuiID id, bool on, float speed)
{
    return To(id, on ? 1.0f : 0.0f, speed);
}

void Blaze::Anim::Set(ImGuiID id, float value)
{
    AnimValues()[id] = value;
}

float Blaze::Anim::EaseOutCubic(float t)
{
    return 1.0f - std::pow(1.0f - std::clamp(t, 0.0f, 1.0f), 3.0f);
}

float Blaze::Anim::EaseInOutCubic(float t)
{
    t = std::clamp(t, 0.0f, 1.0f);
    return t < 0.5f ? 4.0f * t * t * t : 1.0f - std::pow(-2.0f * t + 2.0f, 3.0f) / 2.0f;
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// theme
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    struct ThemeState
    {
        Blaze::Palette dark = Blaze::Palette::DefaultDark();
        Blaze::Palette light = Blaze::Palette::DefaultLight();
        Blaze::Palette from = light;
        Blaze::Palette colors = light;
        float progress = 1.0f;
        Blaze::ThemeMode mode = Blaze::ThemeMode::Light;
        float rounding = 4.0f;
        std::function<void(Blaze::ThemeMode)> changed;

        const Blaze::Palette& Target() const { return mode == Blaze::ThemeMode::Dark ? dark : light; }
    };

    ThemeState& T()
    {
        static ThemeState state;
        return state;
    }

    constexpr float THEME_TRANSITION_SECONDS = 0.32f;

    void ApplyColors(const Blaze::Palette& p)
    {
        ImVec4* c = ImGui::GetStyle().Colors;
        const ImVec4 none(0, 0, 0, 0);
        const auto A = [](const ImVec4& v, float a) { return ImVec4(v.x, v.y, v.z, v.w * a); };

        c[ImGuiCol_Text] = p.Text;
        c[ImGuiCol_TextDisabled] = p.TextMuted;
        c[ImGuiCol_WindowBg] = p.Background;
        c[ImGuiCol_ChildBg] = none;
        c[ImGuiCol_PopupBg] = p.Elevated;
        c[ImGuiCol_Border] = p.Border;
        c[ImGuiCol_BorderShadow] = none;
        c[ImGuiCol_FrameBg] = p.Surface;
        c[ImGuiCol_FrameBgHovered] = p.Elevated;
        c[ImGuiCol_FrameBgActive] = p.Elevated;
        c[ImGuiCol_TitleBg] = p.Background;
        c[ImGuiCol_TitleBgActive] = p.Background;
        c[ImGuiCol_TitleBgCollapsed] = p.Background;
        c[ImGuiCol_MenuBarBg] = p.Sidebar;
        c[ImGuiCol_ScrollbarBg] = none;
        c[ImGuiCol_ScrollbarGrab] = p.FieldActive;
        c[ImGuiCol_ScrollbarGrabHovered] = A(p.TextFaint, 0.7f);
        c[ImGuiCol_ScrollbarGrabActive] = p.TextFaint;
        c[ImGuiCol_CheckMark] = p.Accent;
        c[ImGuiCol_SliderGrab] = p.Accent;
        c[ImGuiCol_SliderGrabActive] = p.AccentHover;
        c[ImGuiCol_Button] = p.Field;
        c[ImGuiCol_ButtonHovered] = p.FieldHover;
        c[ImGuiCol_ButtonActive] = p.FieldActive;
        c[ImGuiCol_Header] = p.Field;
        c[ImGuiCol_HeaderHovered] = A(p.Field, 0.7f);
        c[ImGuiCol_HeaderActive] = p.FieldHover;
        c[ImGuiCol_Separator] = p.Border;
        c[ImGuiCol_SeparatorHovered] = A(p.Accent, 0.6f);
        c[ImGuiCol_SeparatorActive] = p.Accent;
        c[ImGuiCol_ResizeGrip] = none;
        c[ImGuiCol_ResizeGripHovered] = A(p.Accent, 0.35f);
        c[ImGuiCol_ResizeGripActive] = A(p.Accent, 0.6f);
        c[ImGuiCol_Tab] = none;
        c[ImGuiCol_TabHovered] = p.FieldHover;
        c[ImGuiCol_TabSelected] = p.Field;
        c[ImGuiCol_TabSelectedOverline] = p.Accent;
        c[ImGuiCol_TabDimmed] = none;
        c[ImGuiCol_TabDimmedSelected] = p.Field;
        c[ImGuiCol_TabDimmedSelectedOverline] = none;
        c[ImGuiCol_PlotLines] = p.Accent;
        c[ImGuiCol_PlotLinesHovered] = p.AccentHover;
        c[ImGuiCol_PlotHistogram] = p.Accent;
        c[ImGuiCol_PlotHistogramHovered] = p.AccentHover;
        c[ImGuiCol_TableHeaderBg] = p.Surface;
        c[ImGuiCol_TableBorderStrong] = p.Border;
        c[ImGuiCol_TableBorderLight] = p.Border;
        c[ImGuiCol_TableRowBg] = none;
        c[ImGuiCol_TableRowBgAlt] = A(p.Text, 0.025f);
        c[ImGuiCol_TextLink] = p.Accent;
        c[ImGuiCol_TextSelectedBg] = A(p.Accent, 0.3f);
        c[ImGuiCol_DragDropTarget] = p.Accent;
        c[ImGuiCol_NavCursor] = A(p.Accent, 0.8f);
        c[ImGuiCol_NavWindowingHighlight] = A(p.Text, 0.6f);
        c[ImGuiCol_NavWindowingDimBg] = ImVec4(0, 0, 0, 0.3f);
        c[ImGuiCol_ModalWindowDimBg] = ImVec4(0, 0, 0, Blaze::Theme::IsDark() ? 0.45f : 0.25f);
    }
}

Blaze::ThemeMode Blaze::Theme::Mode() { return T().mode; }
bool Blaze::Theme::IsDark() { return T().mode == ThemeMode::Dark; }
const Blaze::Palette& Blaze::Theme::Colors() { return T().colors; }
float Blaze::Theme::Rounding() { return T().rounding; }

void Blaze::Theme::Set(ThemeMode mode, bool animate)
{
    ThemeState& t = T();

    if (mode == t.mode && t.progress >= 1.0f)
        return;

    t.from = t.colors;
    t.mode = mode;
    t.progress = animate ? 0.0f : 1.0f;

    if (!animate)
    {
        t.colors = t.Target();
        if (ImGui::GetCurrentContext())
            ApplyColors(t.colors);
    }

    if (t.changed)
        t.changed(mode);
}

void Blaze::Theme::Toggle()
{
    Set(IsDark() ? ThemeMode::Light : ThemeMode::Dark);
}

void Blaze::Theme::SetAccent(const ImVec4& accent, bool animate)
{
    ThemeState& t = T();
    t.from = t.colors;
    t.dark = t.dark.WithAccent(accent, false);
    t.light = t.light.WithAccent(accent, true);
    t.progress = animate ? 0.0f : 1.0f;

    if (!animate)
    {
        t.colors = t.Target();
        if (ImGui::GetCurrentContext())
            ApplyColors(t.colors);
    }
}

void Blaze::Theme::Customize(ThemeMode mode, const std::function<void(Palette&)>& edit)
{
    ThemeState& t = T();
    t.from = t.colors;
    edit(mode == ThemeMode::Dark ? t.dark : t.light);
    t.progress = 0.0f;
}

void Blaze::Theme::OnChanged(std::function<void(ThemeMode)> callback)
{
    T().changed = std::move(callback);
}

ImU32 Blaze::Theme::U32(const ImVec4& color, float alpha)
{
    return ImGui::ColorConvertFloat4ToU32(ImVec4(color.x, color.y, color.z, color.w * alpha * ImGui::GetStyle().Alpha));
}

void Blaze::Theme::ApplyStyle(float scale)
{
    // From a default style each time, so applying a new scale never compounds the previous one.
    ImGuiStyle& style = ImGui::GetStyle();
    const ImGuiStyle previous = style;
    style = ImGuiStyle();
    style.FontSizeBase = previous.FontSizeBase;
    style.FontScaleMain = previous.FontScaleMain;
    style.FontScaleDpi = previous.FontScaleDpi;

    style.WindowRounding = 8;
    style.ChildRounding = 6;
    style.FrameRounding = 4;
    style.PopupRounding = 5;
    style.GrabRounding = 3;
    style.TabRounding = 4;
    style.ScrollbarRounding = 9;
    style.WindowBorderSize = 1;
    style.ChildBorderSize = 1;
    style.PopupBorderSize = 1;
    style.FrameBorderSize = 1;
    style.TabBorderSize = 0;
    style.TabBarBorderSize = 1;
    style.TabBarOverlineSize = 2;
    style.WindowPadding = ImVec2(16, 14);
    style.FramePadding = ImVec2(10, 8);
    style.ItemSpacing = ImVec2(10, 8);
    style.ItemInnerSpacing = ImVec2(8, 6);
    style.CellPadding = ImVec2(10, 6);
    style.IndentSpacing = 18;
    style.ScrollbarSize = 6;
    style.GrabMinSize = 10;
    style.WindowMinSize = ImVec2(160, 80);
    style.SeparatorTextBorderSize = 1;
    style.SeparatorTextPadding = ImVec2(0, 6);
    style.WindowTitleAlign = ImVec2(0, 0.5f);
    style.ButtonTextAlign = ImVec2(0.5f, 0.5f);
    style.HoverDelayShort = 0.25f;
    style.HoverDelayNormal = 0.45f;
    style.AntiAliasedFill = true;
    style.AntiAliasedLines = true;

    if (std::fabs(scale - 1.0f) > 0.001f)
        style.ScaleAllSizes(scale);

    T().rounding = style.FrameRounding;
    ApplyColors(T().colors);
}

void Blaze::Theme::Update()
{
    ThemeState& t = T();

    if (t.progress >= 1.0f)
        return;

    t.progress = std::min(1.0f, t.progress + Anim::Delta() / THEME_TRANSITION_SECONDS);
    t.colors = t.progress >= 1.0f ? t.Target() : Palette::Lerp(t.from, t.Target(), Anim::EaseInOutCubic(t.progress));
    ApplyColors(t.colors);
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// fonts
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    float g_bodySize = 15.0f;
    ImFont* g_weights[4] = {};   // FontWeight order

    // The icons own their codepoints: a text font must never answer for them (sources are searched in order).
    const ImWchar* IconRange()
    {
        static const ImWchar range[] = { ICON_MIN_LC, ICON_MAX_LC, 0 };
        return range;
    }

    ImFont* AddWeight(const unsigned int* data, unsigned int size, float px, const char* name, const std::function<void(ImFont*)>& merge)
    {
        ImGuiIO& io = ImGui::GetIO();

        ImFontConfig text{};
        text.FontDataOwnedByAtlas = false;   // the arrays are static
        text.OversampleH = 2;
        text.OversampleV = 1;
        text.PixelSnapH = true;
        text.GlyphExcludeRanges = IconRange();
        snprintf(text.Name, sizeof(text.Name), "%s", name);
        ImFont* font = io.Fonts->AddFontFromMemoryTTF(const_cast<unsigned int*>(data), static_cast<int>(size), px, &text);

        // Icons are square: a fixed advance keeps icon columns aligned, and a small downward offset centers the glyph
        // on the text's x-height instead of the baseline.
        ImFontConfig icons{};
        icons.MergeMode = true;
        icons.FontDataOwnedByAtlas = false;
        icons.OversampleH = 1;
        icons.OversampleV = 1;
        icons.PixelSnapH = true;
        icons.GlyphMinAdvanceX = px;
        icons.GlyphOffset = ImVec2(0.0f, std::round(px * 0.14f));
        snprintf(icons.Name, sizeof(icons.Name), "Lucide (%s)", name);
        io.Fonts->AddFontFromMemoryTTF(const_cast<unsigned int*>(Blaze::FontData::Lucide), static_cast<int>(Blaze::FontData::LucideSize), px,
            &icons, IconRange());

        if (merge)
            merge(font);

        return font;
    }
}

float Blaze::Font::Px() const
{
    const ImGuiStyle& style = ImGui::GetStyle();
    return size * style.FontScaleMain * style.FontScaleDpi;
}

ImVec2 Blaze::Font::CalcTextSize(const char* text, const char* textEnd, float wrapWidth) const
{
    return font->CalcTextSizeA(Px(), FLT_MAX, wrapWidth, text, textEnd);
}

float Blaze::Fonts::BodySize() { return g_bodySize; }

void Blaze::Fonts::Load(float bodySize, const std::function<void(ImFont*)>& merge)
{
    using namespace FontData;
    const float body = std::round(bodySize);
    g_bodySize = body;

    // The first font added is ImGui's default font.
    g_weights[0] = AddWeight(InterRegular, InterRegularSize, body, "Inter Regular", merge);
    g_weights[1] = AddWeight(InterMedium, InterMediumSize, body, "Inter Medium", merge);
    g_weights[2] = AddWeight(InterSemiBold, InterSemiBoldSize, body, "Inter SemiBold", merge);
    g_weights[3] = AddWeight(InterBold, InterBoldSize, body, "Inter Bold", merge);

    Regular = { g_weights[0], body };
    Medium = { g_weights[1], body };
    SemiBold = { g_weights[2], body };
    Small = { g_weights[0], body - 2.0f };
    SmallSemiBold = { g_weights[2], body - 2.0f };
    Heading = { g_weights[2], std::round(body * 1.34f) };
    Title = { g_weights[1], std::round(body * 1.27f) };
    Brand = { g_weights[2], std::round(body * 1.87f) };
    IconLarge = { g_weights[0], std::round(body * 1.4f) };

    ImGui::GetIO().FontDefault = g_weights[0];
    ImGui::GetStyle().FontSizeBase = body;
}

Blaze::Font Blaze::Fonts::Add(FontWeight weight, float size)
{
    return { g_weights[static_cast<int>(weight)], size };
}

void Blaze::Fonts::Push(const Font& font)
{
    ImGui::PushFont(font.font, font.size);
}

void Blaze::Fonts::Pop()
{
    ImGui::PopFont();
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// input
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    bool g_keysPrevious[256] = {};
    bool g_keysCurrent[256] = {};
}

void Blaze::Input::Update()
{
    for (int key = 1; key < 256; ++key)
    {
        g_keysPrevious[key] = g_keysCurrent[key];
#ifdef _WIN32
        g_keysCurrent[key] = (GetAsyncKeyState(key) & 0x8000) != 0;
#endif
    }
}

bool Blaze::Input::IsDown(int key) { return key > 0 && key < 256 && g_keysCurrent[key]; }
bool Blaze::Input::Pressed(int key) { return key > 0 && key < 256 && g_keysCurrent[key] && !g_keysPrevious[key]; }
bool Blaze::Input::Released(int key) { return key > 0 && key < 256 && !g_keysCurrent[key] && g_keysPrevious[key]; }

int Blaze::Input::AnyPressed()
{
    for (int key = 1; key < 256; ++key)
    {
        // Shift/Ctrl/Alt report a generic and a left/right code; keep the specific one.
        if (key == 0x10 || key == 0x11 || key == 0x12)
            continue;
        if (Pressed(key))
            return key;
    }
    return 0;
}

std::string Blaze::Input::KeyName(int key)
{
    struct Named { int key; const char* name; };
    static constexpr Named names[] = {
        { 0x01, "Mouse 1" }, { 0x02, "Mouse 2" }, { 0x04, "Mouse 3" }, { 0x05, "Mouse 4" }, { 0x06, "Mouse 5" },
        { 0x08, "Back" }, { 0x09, "Tab" }, { 0x0D, "Enter" }, { 0x10, "Shift" }, { 0x11, "Control" }, { 0x12, "Alt" },
        { 0x13, "Pause" }, { 0x14, "Caps Lock" }, { 0x1B, "Escape" }, { 0x20, "Space" }, { 0x21, "Page Up" },
        { 0x22, "Page Down" }, { 0x23, "End" }, { 0x24, "Home" }, { 0x25, "Left" }, { 0x26, "Up" }, { 0x27, "Right" },
        { 0x28, "Down" }, { 0x2D, "Insert" }, { 0x2E, "Delete" }, { 0xA0, "L Shift" }, { 0xA1, "R Shift" },
        { 0xA2, "L Ctrl" }, { 0xA3, "R Ctrl" }, { 0xA4, "L Alt" }, { 0xA5, "R Alt" },
    };

    if (key == 0)
        return "None";
    for (const Named& named : names)
        if (named.key == key)
            return named.name;

    char text[16];
    if ((key >= '0' && key <= '9') || (key >= 'A' && key <= 'Z'))
        snprintf(text, sizeof(text), "%c", key);
    else if (key >= 0x60 && key <= 0x69)
        snprintf(text, sizeof(text), "Num %d", key - 0x60);
    else if (key >= 0x70 && key <= 0x87)
        snprintf(text, sizeof(text), "F%d", key - 0x70 + 1);
    else
        snprintf(text, sizeof(text), "Key 0x%02X", key);
    return text;
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// widgets: layout
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    ImGuiID g_capturingKey = 0;

    // A control's label on its own line, a little closer to the control than to the row above.
    void Label(const char* label)
    {
        const std::string text = Blaze::Ui::Visible(label);
        if (text.empty())
            return;
        ImGui::TextUnformatted(text.c_str());
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() - S(1));
    }

    // Extra room after a labeled control, so rows read as separate groups.
    void Gap() { ImGui::Dummy(ImVec2(0.0f, S(8))); }
}

std::string Blaze::Ui::Visible(const char* label)
{
    if (!label)
        return {};

    const char* end = std::strstr(label, "##");
    return end ? std::string(label, end) : std::string(label);
}

ImVec4 Blaze::Ui::ToneColor(Tone tone)
{
    switch (tone)
    {
    case Tone::Accent: return P().Accent;
    case Tone::Success: return P().Success;
    case Tone::Warning: return P().Warning;
    case Tone::Danger: return P().Danger;
    default: return P().TextMuted;
    }
}

void Blaze::Ui::Text(const char* text, const ImVec4& color)
{
    ImGui::PushStyleColor(ImGuiCol_Text, color);
    ImGui::TextUnformatted(text);
    ImGui::PopStyleColor();
}

void Blaze::Ui::Text(const char* text, Tone tone)
{
    Text(text, ToneColor(tone));
}

void Blaze::Ui::TextWrapped(const char* text)
{
    ImGui::PushTextWrapPos(0.0f);
    ImGui::TextUnformatted(text);
    ImGui::PopTextWrapPos();
}

void Blaze::Ui::Caption(const char* text)
{
    std::string upper = Visible(text);
    std::transform(upper.begin(), upper.end(), upper.begin(), [](unsigned char ch) { return static_cast<char>(std::toupper(ch)); });
    Fonts::Push(Fonts::Small);
    Text(upper.c_str(), P().TextFaint);
    Fonts::Pop();
    ImGui::Dummy(ImVec2(0.0f, S(4)));
}

void Blaze::Ui::Section(const char* title)
{
    if (ImGui::GetCursorPosY() > ImGui::GetStyle().WindowPadding.y + 1.0f)
        ImGui::Dummy(ImVec2(0.0f, S(6)));
    Caption(title);
}

void Blaze::Ui::Readout(const char* label, const char* value, Tone tone)
{
    const float x0 = ImGui::GetCursorPosX();
    const float split = std::max(S(110), ImGui::GetContentRegionAvail().x * ReadoutSplit);
    // A little more air than plain text lines (added above, so nothing extends past the last row).
    ImGui::SetCursorPosY(ImGui::GetCursorPosY() + S(3));
    Text(label, P().TextMuted);
    ImGui::SameLine(x0 + split);
    Text(value, tone == Tone::Neutral ? P().Text : ToneColor(tone));
}

void Blaze::Ui::About(const char* description)
{
    if (!description)
        return;
    if (ImGui::IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
        CurrentAbout = description;
    if (!InlineDescriptions)
        return;
    Fonts::Push(Fonts::Small);
    ImGui::PushStyleColor(ImGuiCol_Text, P().TextMuted);
    ImGui::SetCursorPosY(ImGui::GetCursorPosY() - S(3));
    TextWrapped(description);
    ImGui::PopStyleColor();
    Fonts::Pop();
}

void Blaze::Ui::BeginCard(const char* id, const char* title, const char* icon, float width)
{
    ImGui::PushStyleColor(ImGuiCol_ChildBg, P().Surface);
    ImGui::PushStyleColor(ImGuiCol_Border, P().Border);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(14, 12));
    ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, S(6));
    ImGui::BeginChild(id, ImVec2(width, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysUseWindowPadding,
        ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleVar(2);
    ImGui::PopStyleColor(2);

    if (!title)
        return;

    Fonts::Push(Fonts::Medium);
    if (icon)
    {
        Text(icon, P().TextMuted);
        ImGui::SameLine(0.0f, S(8));
    }
    ImGui::TextUnformatted(title);
    Fonts::Pop();
    // No spacer: the title stays the last item, so SameLineRight can put a badge or button beside it.
}

void Blaze::Ui::EndCard()
{
    ImGui::EndChild();
}

bool Blaze::Ui::BeginGrid(const char* id, int columns)
{
    ImGui::PushStyleVar(ImGuiStyleVar_CellPadding, S(6, 6));
    const bool open = ImGui::BeginTable(id, columns, ImGuiTableFlags_SizingStretchSame | ImGuiTableFlags_NoPadOuterX);
    ImGui::PopStyleVar();
    return open;
}

void Blaze::Ui::NextCell() { ImGui::TableNextColumn(); }
void Blaze::Ui::EndGrid() { ImGui::EndTable(); }

void Blaze::Ui::Divider()
{
    ImGui::Dummy(ImVec2(0.0f, S(2)));
    const ImVec2 start = ImGui::GetCursorScreenPos();
    const float width = ImGui::GetContentRegionAvail().x;
    ImGui::GetWindowDrawList()->AddLine(start, ImVec2(start.x + width, start.y), Col(P().Border), 1.0f);
    ImGui::Dummy(ImVec2(width, S(6)));
}

void Blaze::Ui::Hint(const char* text)
{
    ImGui::PushStyleColor(ImGuiCol_Text, P().TextMuted);
    TextWrapped(text);
    ImGui::PopStyleColor();
}

void Blaze::Ui::SameLineRight(float width)
{
    ImGui::SameLine();
    ImGui::SetCursorPosX(ImGui::GetCursorPosX() + std::max(0.0f, ImGui::GetContentRegionAvail().x - width));
}

ImVec2 Blaze::Ui::BadgeSize(const char* text)
{
    Fonts::Push(Fonts::Small);
    const ImVec2 size = ImGui::CalcTextSize(text) + S(7, 2) * 2.0f;
    Fonts::Pop();
    return size;
}

void Blaze::Ui::Badge(const char* text, Tone tone)
{
    Fonts::Push(Fonts::Small);
    const ImVec2 size = ImGui::CalcTextSize(text);
    const ImVec2 padding = S(7, 2);
    const ImVec2 box = size + padding * 2.0f;
    ImVec2 start = ImGui::GetCursorScreenPos();
    const float lineHeight = ImGui::GetTextLineHeightWithSpacing();

    // Centered on the item it follows when it shares its top edge (after SameLine), else on a frame-height line.
    const ImVec2 previousMin = ImGui::GetItemRectMin();
    const ImVec2 previousMax = ImGui::GetItemRectMax();
    if (std::fabs(previousMin.y - start.y) < 1.0f && previousMax.y > previousMin.y)
        start.y = (previousMin.y + previousMax.y - box.y) * 0.5f;
    else
        start.y += std::max(0.0f, (ImGui::GetFrameHeight() - box.y) * 0.5f);

    const ImVec4 color = tone == Tone::Neutral ? P().TextMuted : ToneColor(tone);
    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(start, start + box, Col(color, Theme::IsDark() ? 0.16f : 0.12f), box.y * 0.5f);
    draw->AddText(start + padding, Col(color), text);
    ImGui::Dummy(ImVec2(box.x, std::min(box.y, lineHeight)));
    Fonts::Pop();
}

void Blaze::Ui::Stat(const char* label, const char* value, const char* change, Tone changeTone, const char* icon)
{
    const float width = ImGui::GetContentRegionAvail().x;
    const float height = S(78);
    const ImVec2 start = ImGui::GetCursorScreenPos();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(start, start + ImVec2(width, height), Col(P().Surface), S(6));
    draw->AddRect(start, start + ImVec2(width, height), Col(P().Border), S(6));

    const ImVec2 pad = S(14, 12);
    const std::string labelText = icon ? std::string(icon) + "  " + label : std::string(label);
    draw->AddText(Fonts::Small.font, Fonts::Small.Px(), start + pad, Col(P().TextMuted), labelText.c_str());
    const ImVec2 valuePos = start + pad + ImVec2(0.0f, Fonts::Small.Px() + S(8));
    draw->AddText(Fonts::Heading.font, Fonts::Heading.Px(), valuePos, Col(P().Text), value);

    if (change)
    {
        const float valueWidth = Fonts::Heading.CalcTextSize(value).x;
        const float y = valuePos.y + Fonts::Heading.Px() - Fonts::Small.Px() - S(1);
        draw->AddText(Fonts::SmallSemiBold.font, Fonts::SmallSemiBold.Px(), ImVec2(valuePos.x + valueWidth + S(8), y),
            Col(ToneColor(changeTone)), change);
    }

    ImGui::Dummy(ImVec2(width, height));
}

void Blaze::Ui::LineChart(const char* id, const float* values, int count, float height, std::optional<float> min, std::optional<float> max,
    const char* valueFormat, Tone tone)
{
    if (height <= 0.0f)
        height = S(120);

    const float width = ImGui::GetContentRegionAvail().x;
    const ImVec2 start = ImGui::GetCursorScreenPos();
    ImGui::InvisibleButton(id, ImVec2(std::max(1.0f, width), height));
    const bool hovered = ImGui::IsItemHovered();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec4 color = ToneColor(tone);

    // three faint guide lines
    for (int i = 0; i <= 2; ++i)
    {
        const float y = start.y + S(4) + (height - S(8)) * static_cast<float>(i) / 2.0f;
        draw->AddLine(ImVec2(start.x, y), ImVec2(start.x + width, y), Col(P().Border), 1.0f);
    }

    if (count < 2)
        return;

    float low = min.value_or(FLT_MAX);
    float high = max.value_or(-FLT_MAX);
    for (int i = 0; i < count; ++i)
    {
        if (!min) low = std::min(low, values[i]);
        if (!max) high = std::max(high, values[i]);
    }
    if (high - low < 1e-6f)
        high = low + 1.0f;

    const float top = start.y + S(6);
    const float bottom = start.y + height - S(6);
    std::vector<ImVec2> points(static_cast<size_t>(count));
    for (int i = 0; i < count; ++i)
    {
        const float x = start.x + width * static_cast<float>(i) / static_cast<float>(count - 1);
        const float y = bottom - (bottom - top) * std::clamp((values[i] - low) / (high - low), 0.0f, 1.0f);
        points[i] = ImVec2(x, y);
    }

    // The area is drawn as quads with anti-aliased fill off, so adjacent quads leave no seams.
    const ImDrawListFlags flags = draw->Flags;
    draw->Flags &= ~ImDrawListFlags_AntiAliasedFill;
    const ImU32 fill = Col(color, Theme::IsDark() ? 0.12f : 0.10f);
    for (int i = 0; i < count - 1; ++i)
        draw->AddQuadFilled(points[i], points[i + 1], ImVec2(points[i + 1].x, bottom), ImVec2(points[i].x, bottom), fill);
    draw->Flags = flags;
    draw->AddPolyline(points.data(), count, Col(color), ImDrawFlags_None, S(2));

    if (!hovered)
        return;

    const float mouse = ImGui::GetIO().MousePos.x;
    const int index = std::clamp(static_cast<int>(std::round((mouse - start.x) / width * static_cast<float>(count - 1))), 0, count - 1);
    const ImVec2 point = points[index];
    draw->AddLine(ImVec2(point.x, top), ImVec2(point.x, bottom), Col(P().TextFaint, 0.6f), 1.0f);
    draw->AddCircleFilled(point, S(5), Col(color));
    draw->AddCircleFilled(point, S(2.5f), Col(P().Background));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(10, 6));
    ImGui::BeginTooltip();
    ImGui::TextUnformatted(Format(valueFormat, values[index]).c_str());
    ImGui::EndTooltip();
    ImGui::PopStyleVar();
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// widgets: controls
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    float CheckSize() { return std::round(S(14)); }

    // A square box with a hairline border; when on, a smaller ink square grows in its middle.
    void DrawCheck(const ImVec2& start, float height, const std::string& text, bool value, bool hovered)
    {
        using namespace Blaze;
        const float on = Anim::Toggle(ImGui::GetID("##on"), value, 20.0f);
        const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered, 18.0f);
        ImDrawList* draw = ImGui::GetWindowDrawList();
        const float box = CheckSize();
        const ImVec2 min(start.x, std::round(start.y + (height - box) * 0.5f));
        draw->AddRectFilled(min, min + ImVec2(box, box), Col(Anim::Lerp(P().Surface, P().Elevated, hover)), S(3));
        draw->AddRect(min, min + ImVec2(box, box), Col(Anim::Lerp(P().Border, P().TextFaint, hover * 0.6f)), S(3));
        if (on > 0.01f)
        {
            const float inner = Anim::Lerp(S(2), S(8), Anim::EaseOutCubic(on));
            const ImVec2 center = min + ImVec2(box * 0.5f, box * 0.5f);
            draw->AddRectFilled(center - ImVec2(inner, inner) * 0.5f, center + ImVec2(inner, inner) * 0.5f, Col(P().Accent, on), S(1.5f));
        }
        if (!text.empty())
            draw->AddText(ImVec2(min.x + box + S(10), start.y + (height - ImGui::GetTextLineHeight()) * 0.5f), Col(P().Text), text.c_str());
    }
}

bool Blaze::Ui::Toggle(const char* label, bool* value, const char* description)
{
    ImGui::PushID(label);
    const float width = std::max(1.0f, ImGui::GetContentRegionAvail().x);
    const float height = ImGui::GetFrameHeight();
    const ImVec2 start = ImGui::GetCursorScreenPos();
    const bool changed = ImGui::InvisibleButton("##row", ImVec2(width, height));
    if (changed)
        *value = !*value;
    const bool hovered = ImGui::IsItemHovered();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
    DrawCheck(start, height, Visible(label), *value, hovered);
    ImGui::PopID();
    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::Checkbox(const char* label, bool* value)
{
    ImGui::PushID(label);
    const std::string text = Visible(label);
    const float height = ImGui::GetFrameHeight();
    const float width = CheckSize() + (!text.empty() ? S(10) + ImGui::CalcTextSize(text.c_str()).x : 0.0f);
    const ImVec2 start = ImGui::GetCursorScreenPos();
    const bool changed = ImGui::InvisibleButton("##box", ImVec2(width, height));
    if (changed)
        *value = !*value;
    const bool hovered = ImGui::IsItemHovered();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
    DrawCheck(start, height, text, *value, hovered);
    ImGui::PopID();
    return changed;
}

namespace
{
    bool SliderCore(const char* label, float* value, float min, float max, float step, const char* shown, const char* description)
    {
        using namespace Blaze;
        ImGui::PushID(label);
        ImGui::BeginGroup();
        Label(label);
        const float width = Ui::ControlWidth();
        const float height = ImGui::GetFrameHeight() + S(4);
        const ImVec2 start = ImGui::GetCursorScreenPos();
        ImDrawList* draw = ImGui::GetWindowDrawList();

        // the value box
        const ImVec2 textSize = ImGui::CalcTextSize(shown);
        const float boxWidth = std::max(S(48), textSize.x + S(20));
        draw->AddRectFilled(start, start + ImVec2(boxWidth, height), Col(P().Surface), S(4));
        draw->AddRect(start, start + ImVec2(boxWidth, height), Col(P().Border), S(4));
        draw->AddText(start + (ImVec2(boxWidth, height) - textSize) * 0.5f, Col(P().Text), shown);

        // the track: a thin rail, an ink fill up to a small grip
        const ImVec2 gripSize = Ui::S(22, 15);
        const float gap = S(30);
        const ImVec2 trackStart(start.x + boxWidth + gap, start.y);
        const float trackWidth = std::max(S(40), width - boxWidth - gap);
        ImGui::SetCursorScreenPos(trackStart);
        ImGui::InvisibleButton("##track", ImVec2(trackWidth, height));
        const bool active = ImGui::IsItemActive();
        const bool hovered = ImGui::IsItemHovered();
        if (hovered || active)
            ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
        const float left = trackStart.x + gripSize.x * 0.5f;
        const float right = trackStart.x + trackWidth - gripSize.x * 0.5f;
        bool changed = false;
        if (active && max > min)
        {
            const float fraction = std::clamp((ImGui::GetIO().MousePos.x - left) / (right - left), 0.0f, 1.0f);
            float next = min + fraction * (max - min);
            if (step > 0.0f)
                next = std::round(next / step) * step;
            if (next != *value)
            {
                *value = next;
                changed = true;
            }
        }
        const float target = max > min ? std::clamp((*value - min) / (max - min), 0.0f, 1.0f) : 0.0f;
        const float t = Anim::To(ImGui::GetID("##fill"), target, active ? 40.0f : 16.0f);
        const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered || active, 18.0f);

        const float cy = std::round(trackStart.y + height * 0.5f);
        const float thick = std::round(S(8));
        const ImVec2 railMin(trackStart.x, cy - thick * 0.5f);
        const ImVec2 railMax(trackStart.x + trackWidth, cy + thick * 0.5f);
        draw->AddRectFilled(railMin, railMax, Col(P().Surface), thick * 0.5f);
        draw->AddRect(railMin, railMax, Col(P().Border), thick * 0.5f);
        const float gripX = Anim::Lerp(left, right, t);
        if (gripX - railMin.x > S(2))
            draw->AddRectFilled(railMin + Ui::S(1, 1), ImVec2(gripX, railMax.y - S(1)), Col(Anim::Lerp(P().Accent, P().AccentHover, hover)), thick * 0.5f);
        const ImVec2 gripMin(std::round(gripX - gripSize.x * 0.5f), std::round(cy - gripSize.y * 0.5f));
        draw->AddRectFilled(gripMin + ImVec2(0.0f, S(1)), gripMin + gripSize + ImVec2(0.0f, S(1)), Col(ImVec4(0, 0, 0, Theme::IsDark() ? 0.3f : 0.08f)), S(4));
        draw->AddRectFilled(gripMin, gripMin + gripSize, Col(P().Knob), S(4));
        draw->AddRect(gripMin, gripMin + gripSize, Col(Anim::Lerp(P().Border, P().TextFaint, hover)), S(4));
        // three grip lines
        for (int i = -1; i <= 1; ++i)
        {
            const float lx = std::round(gripMin.x + gripSize.x * 0.5f + static_cast<float>(i) * S(3)) + 0.5f;
            draw->AddLine(ImVec2(lx, gripMin.y + S(4.5f)), ImVec2(lx, gripMin.y + gripSize.y - S(4.5f)), Col(P().TextMuted), std::max(1.0f, S(1)));
        }

        ImGui::SetCursorScreenPos(start);
        ImGui::Dummy(ImVec2(width, height));
        ImGui::EndGroup();
        ImGui::PopID();
        Ui::About(description);
        Gap();
        return changed;
    }
}

bool Blaze::Ui::Slider(const char* label, float* value, float min, float max, const char* format, const char* description)
{
    return SliderCore(label, value, min, max, 0.0f, Format(format, *value).c_str(), description);
}

bool Blaze::Ui::Slider(const char* label, int* value, int min, int max, const char* format, const char* description)
{
    char shown[64];
    snprintf(shown, sizeof(shown), format, *value);
    float f = static_cast<float>(*value);
    if (!SliderCore(label, &f, static_cast<float>(min), static_cast<float>(max), 1.0f, shown, description))
        return false;
    *value = static_cast<int>(std::round(f));
    return true;
}

bool Blaze::Ui::Button(const char* label, ButtonKind kind, ImVec2 size)
{
    const std::string text = Visible(label);
    const ImVec2 textSize = ImGui::CalcTextSize(text.c_str());
    const ImVec2 padding = ImGui::GetStyle().FramePadding;
    if (size.x < 0.0f)
        size.x = std::max(1.0f, ImGui::GetContentRegionAvail().x);
    if (size.x == 0.0f)
        size.x = textSize.x + padding.x * 2.0f + S(10);
    if (size.y == 0.0f)
        size.y = ImGui::GetFrameHeight();

    const ImVec2 start = ImGui::GetCursorScreenPos();
    const bool clicked = ImGui::InvisibleButton(label, size);
    const bool hovered = ImGui::IsItemHovered();
    const ImGuiID id = ImGui::GetItemID();
    const float hover = Anim::Toggle(id ^ 0x5A17u, hovered, 18.0f);
    const float press = Anim::Toggle(id ^ 0x7E11u, ImGui::IsItemActive(), 28.0f);
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

    ImVec4 background, foreground, border;
    switch (kind)
    {
    case ButtonKind::Primary:
        background = Anim::Lerp(Anim::Lerp(P().Accent, P().AccentHover, hover), P().Accent, press);
        foreground = P().OnAccent;
        border = Palette::Alpha(P().Accent, 0.0f);
        break;
    case ButtonKind::Ghost:
        background = Anim::Lerp(Palette::Alpha(P().Field, 0.0f), P().Field, std::max(hover, press));
        foreground = Anim::Lerp(P().TextMuted, P().Text, hover);
        border = Palette::Alpha(P().Border, 0.0f);
        break;
    case ButtonKind::Danger:
        background = Palette::Alpha(P().Danger, Anim::Lerp(0.06f, 0.12f, hover) + 0.06f * press);
        foreground = P().Danger;
        border = Palette::Alpha(P().Danger, 0.35f);
        break;
    default:
        background = Anim::Lerp(Anim::Lerp(P().Surface, P().Elevated, hover), P().Field, press);
        foreground = P().Text;
        border = Anim::Lerp(P().Border, P().TextFaint, hover * 0.7f);
        break;
    }

    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float rounding = ImGui::GetStyle().FrameRounding;
    draw->AddRectFilled(start, start + size, Col(background), rounding);
    if (border.w > 0.0f)
        draw->AddRect(start, start + size, Col(border), rounding);
    draw->AddText(start + (size - textSize) * 0.5f, Col(foreground), text.c_str());
    return clicked;
}

bool Blaze::Ui::IconButton(const char* icon, const char* id, const char* tooltip, float size)
{
    if (size <= 0.0f)
        size = ImGui::GetFrameHeight();

    const ImVec2 start = ImGui::GetCursorScreenPos();
    const bool clicked = ImGui::InvisibleButton(id, ImVec2(size, size));
    const bool hovered = ImGui::IsItemHovered();
    const float hover = Anim::Toggle(ImGui::GetItemID() ^ 0x1C0Bu, hovered, 18.0f);
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(start, start + ImVec2(size, size), Col(P().Field, hover), ImGui::GetStyle().FrameRounding);
    const ImVec2 iconSize = ImGui::CalcTextSize(icon);
    draw->AddText(start + (ImVec2(size, size) - iconSize) * 0.5f, Col(Anim::Lerp(P().TextMuted, P().Text, hover)), icon);

    if (tooltip)
        Tooltip(tooltip);
    return clicked;
}

bool Blaze::Ui::Segmented(const char* id, int* index, const char* const* items, int count, float width)
{
    if (count <= 0)
        return false;

    ImGui::PushID(id);
    if (width <= 0.0f)
        width = std::max(1.0f, ControlWidth());

    const float height = ImGui::GetFrameHeight();
    const ImVec2 start = ImGui::GetCursorScreenPos();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float rounding = ImGui::GetStyle().FrameRounding;
    const float inset = S(3);
    const float segment = (width - inset * 2.0f) / static_cast<float>(count);
    draw->AddRectFilled(start, start + ImVec2(width, height), Col(P().Surface), rounding);
    draw->AddRect(start, start + ImVec2(width, height), Col(P().Border), rounding);

    const float at = Anim::To(ImGui::GetID("##thumb"), static_cast<float>(*index), 16.0f);
    const ImVec2 thumbMin(start.x + inset + segment * at, start.y + inset);
    const ImVec2 thumbMax = thumbMin + ImVec2(segment, height - inset * 2.0f);
    draw->AddRectFilled(thumbMin, thumbMax, Col(P().Accent), std::max(0.0f, rounding - inset * 0.5f));

    bool changed = false;
    for (int i = 0; i < count; ++i)
    {
        const ImVec2 itemMin(start.x + inset + segment * static_cast<float>(i), start.y);
        ImGui::SetCursorScreenPos(itemMin);
        ImGui::PushID(i);
        if (ImGui::InvisibleButton("##seg", ImVec2(std::max(1.0f, segment), height)) && *index != i)
        {
            *index = i;
            changed = true;
        }
        ImGui::PopID();
        const bool hovered = ImGui::IsItemHovered();
        if (hovered)
            ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

        const float selected = std::clamp(1.0f - std::fabs(at - static_cast<float>(i)), 0.0f, 1.0f);
        const float hover = Anim::Toggle(ImGui::GetItemID(), hovered, 18.0f);
        const ImVec4 color = Anim::Lerp(Anim::Lerp(P().TextMuted, P().Text, hover), P().OnAccent, selected);
        const std::string text = Visible(items[i]);
        const ImVec2 size = ImGui::CalcTextSize(text.c_str());
        draw->AddText(ImVec2(itemMin.x + (segment - size.x) * 0.5f, itemMin.y + (height - size.y) * 0.5f), Col(color), text.c_str());
    }

    ImGui::SetCursorScreenPos(start);
    ImGui::Dummy(ImVec2(width, height));
    ImGui::PopID();
    return changed;
}

float Blaze::Ui::ControlWidth()
{
    return std::min(S(280), ImGui::GetContentRegionAvail().x);
}

void Blaze::Ui::Row(const char* label, float controlWidth)
{
    if (controlWidth <= 0.0f)
        controlWidth = ControlWidth();
    Label(label);
    ImGui::SetNextItemWidth(controlWidth);
}

bool Blaze::Ui::Combo(const char* label, int* index, const char* const* items, int count, const char* description)
{
    ImGui::PushID(label);
    ImGui::BeginGroup();
    Row(label);
    const float width = ImGui::CalcItemWidth();
    const ImVec2 size(width, ImGui::GetFrameHeight());
    const ImVec2 min = ImGui::GetCursorScreenPos();
    const ImVec2 max = min + size;
    if (ImGui::InvisibleButton("##field", size))
        ImGui::OpenPopup("##list");
    const bool hovered = ImGui::IsItemHovered();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
    const bool open = ImGui::IsPopupOpen("##list");
    const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered || open, 18.0f);

    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float rounding = ImGui::GetStyle().FrameRounding;
    const ImVec2 padding = ImGui::GetStyle().FramePadding;
    draw->AddRectFilled(min, max, Col(Anim::Lerp(P().Surface, P().Elevated, hover)), rounding);
    draw->AddRect(min, max, Col(Anim::Lerp(P().Border, P().TextFaint, hover * 0.7f)), rounding);
    const char* preview = *index >= 0 && *index < count ? items[*index] : "";
    const float textY = min.y + (size.y - ImGui::GetTextLineHeight()) * 0.5f;
    draw->PushClipRect(min, max - ImVec2(padding.x + S(16), 0.0f), true);
    draw->AddText(ImVec2(min.x + padding.x, textY), Col(P().Text), preview);
    draw->PopClipRect();
    const char* chevron = open ? ICON_LC_CHEVRON_UP : ICON_LC_CHEVRON_DOWN;
    const ImVec2 chevronSize = ImGui::CalcTextSize(chevron);
    draw->AddText(ImVec2(max.x - chevronSize.x - padding.x, min.y + (size.y - chevronSize.y) * 0.5f), Col(P().TextMuted), chevron);

    // The list opens just under the field, the selected item highlighted.
    bool changed = false;
    ImGui::SetNextWindowPos(ImVec2(min.x, max.y + S(6)));
    ImGui::SetNextWindowSize(ImVec2(width, 0.0f));
    ImGui::PushStyleVar(ImGuiStyleVar_PopupRounding, S(5));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(6, 6));
    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(0.0f, S(2)));
    ImGui::PushStyleVar(ImGuiStyleVar_SelectableTextAlign, ImVec2(0.0f, 0.5f));
    if (ImGui::BeginPopup("##list", ImGuiWindowFlags_NoMove))
    {
        for (int i = 0; i < count; ++i)
        {
            const bool selected = i == *index;
            ImGui::PushStyleColor(ImGuiCol_Text, selected ? P().Text : P().TextMuted);
            ImGui::PushID(i);
            const std::string text = std::string("  ") + items[i];
            if (ImGui::Selectable(text.c_str(), selected, ImGuiSelectableFlags_None, ImVec2(0.0f, S(28))))
            {
                *index = i;
                changed = true;
            }
            ImGui::PopID();
            ImGui::PopStyleColor();
            if (selected && ImGui::IsWindowAppearing())
                ImGui::SetItemDefaultFocus();
        }
        ImGui::EndPopup();
    }
    ImGui::PopStyleVar(4);
    ImGui::EndGroup();
    ImGui::PopID();
    About(description);
    Gap();
    return changed;
}

namespace
{
    int ResizeString(ImGuiInputTextCallbackData* data)
    {
        if (data->EventFlag == ImGuiInputTextFlags_CallbackResize)
        {
            auto* text = static_cast<std::string*>(data->UserData);
            text->resize(static_cast<size_t>(data->BufTextLen));
            data->Buf = text->data();
        }
        return 0;
    }
}

bool Blaze::Ui::TextInput(const char* label, std::string* value, const char* hint, const char* description)
{
    ImGui::BeginGroup();
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::InputTextWithHint(id.c_str(), hint, value->data(), value->capacity() + 1, ImGuiInputTextFlags_CallbackResize,
        ResizeString, value);
    ImGui::EndGroup();
    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::Number(const char* label, float* value, float speed, float min, float max, const char* format, const char* description)
{
    ImGui::BeginGroup();
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::DragFloat(id.c_str(), value, speed, min, max, format);
    ImGui::EndGroup();
    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::Number(const char* label, int* value, float speed, int min, int max, const char* description)
{
    ImGui::BeginGroup();
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::DragInt(id.c_str(), value, speed, min, max);
    ImGui::EndGroup();
    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::Color(const char* label, ImVec4* color, bool alpha, const char* description)
{
    const float swatch = ImGui::GetFrameHeight();
    ImGui::BeginGroup();
    ImGuiColorEditFlags flags = ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoLabel | ImGuiColorEditFlags_AlphaPreviewHalf;
    flags |= alpha ? ImGuiColorEditFlags_AlphaBar : ImGuiColorEditFlags_NoAlpha;
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(swatch * 0.8f - S(1), ImGui::GetStyle().FramePadding.y));
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::ColorEdit4(id.c_str(), &color->x, flags);
    ImGui::PopStyleVar();
    ImGui::SameLine(0.0f, S(12));
    ImGui::AlignTextToFramePadding();
    ImGui::TextUnformatted(Visible(label).c_str());
    ImGui::EndGroup();
    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::Keybind(const char* label, int* key, const char* description)
{
    const ImGuiID id = ImGui::GetID(label);
    const bool waiting = g_capturingKey == id;
    bool changed = false;

    if (waiting)
    {
        const int pressed = Input::AnyPressed();
        if (pressed == 0x1B)   // Escape
            g_capturingKey = 0;
        else if (pressed == 0x08)   // Backspace
        {
            *key = 0;
            changed = true;
            g_capturingKey = 0;
        }
        else if (pressed != 0)
        {
            *key = pressed;
            changed = true;
            g_capturingKey = 0;
        }
    }

    // An outlined button reading "Label: Key".
    const std::string text = Visible(label) + ": " + (waiting ? std::string("press a key...") : Input::KeyName(*key)) + "##" + label;
    if (Button(text.c_str(), waiting ? ButtonKind::Primary : ButtonKind::Secondary, ImVec2(ControlWidth(), 0.0f)) && !waiting)
        g_capturingKey = id;

    About(description);
    Gap();
    return changed;
}

bool Blaze::Ui::IsCapturingKey()
{
    return g_capturingKey != 0;
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// widgets: feedback
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

void Blaze::Ui::Progress(float fraction, const char* label, Tone tone, float height)
{
    if (height <= 0.0f)
        height = S(6);

    const float width = std::max(1.0f, ImGui::GetContentRegionAvail().x);
    ImGui::PushID(label ? label : "##progress");

    if (label)
    {
        const float x0 = ImGui::GetCursorPosX();
        ImGui::TextUnformatted(Visible(label).c_str());
        char percent[16];
        snprintf(percent, sizeof(percent), "%.0f%%", std::clamp(fraction, 0.0f, 1.0f) * 100.0f);
        ImGui::SameLine(x0 + width - ImGui::CalcTextSize(percent).x);
        Text(percent, P().TextMuted);
    }

    const float shown = Anim::To(ImGui::GetID("##value"), std::clamp(fraction, 0.0f, 1.0f), 10.0f, 0.0f);
    const ImVec2 start = ImGui::GetCursorScreenPos();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(start, start + ImVec2(width, height), Col(P().Field), height);
    if (shown > 0.001f)
        draw->AddRectFilled(start, start + ImVec2(std::max(height, width * shown), height), Col(ToneColor(tone)), height);
    ImGui::Dummy(ImVec2(width, height));
    ImGui::PopID();
}

void Blaze::Ui::Spinner(float radius, float thickness)
{
    if (radius <= 0.0f)
        radius = S(8);
    if (thickness <= 0.0f)
        thickness = S(2);

    const ImVec2 start = ImGui::GetCursorScreenPos();
    const ImVec2 center = start + ImVec2(radius + thickness, radius + thickness);
    const float time = static_cast<float>(ImGui::GetTime());
    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddCircle(center, radius, Col(P().Field), 32, thickness);
    const float head = time * 5.5f;
    const float length = 1.2f + std::sin(time * 2.4f) * 0.7f;
    draw->PathArcTo(center, radius, head, head + length, 24);
    draw->PathStroke(Col(P().Accent), ImDrawFlags_None, thickness);
    ImGui::Dummy(ImVec2((radius + thickness) * 2.0f, (radius + thickness) * 2.0f));
}

void Blaze::Ui::Tooltip(const char* text)
{
    const ImGuiID id = ImGui::GetItemID() ^ 0x7007u;
    if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
    {
        Anim::Set(id, 0.0f);
        return;
    }

    const float alpha = Anim::To(id, 1.0f, 16.0f, 0.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, ImGui::GetStyle().Alpha * alpha);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(10, 7));
    ImGui::PushStyleVar(ImGuiStyleVar_PopupRounding, S(7));
    ImGui::BeginTooltip();
    ImGui::PushTextWrapPos(S(280));
    ImGui::TextUnformatted(Visible(text).c_str());
    ImGui::PopTextWrapPos();
    ImGui::EndTooltip();
    ImGui::PopStyleVar(3);
}

void Blaze::Ui::HelpMarker(const char* text)
{
    Text(ICON_LC_INFO, P().TextFaint);
    Tooltip(text);
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// toasts
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

namespace
{
    struct Toast
    {
        int id = 0;
        std::string title;
        std::string message;
        Blaze::Tone tone = Blaze::Tone::Accent;
        std::string icon;
        float seconds = 3.5f;
        float age = 0.0f;
        float height = 0.0f;
    };

    std::mutex g_toastMutex;
    std::vector<Toast> g_toasts;
    int g_nextToast = 0;
}

void Blaze::Notify::Show(const char* title, const char* message, Tone tone, float seconds, const char* icon)
{
    if (!icon)
        icon = tone == Tone::Success ? ICON_LC_CIRCLE_CHECK : tone == Tone::Warning ? ICON_LC_TRIANGLE_ALERT :
            tone == Tone::Danger ? ICON_LC_CIRCLE_X : ICON_LC_INFO;

    std::lock_guard lock(g_toastMutex);
    g_toasts.push_back({ g_nextToast++, title ? title : "", message ? message : "", tone, icon, seconds });
    if (g_toasts.size() > 6)
        g_toasts.erase(g_toasts.begin());
}

void Blaze::Notify::Draw()
{
    std::lock_guard lock(g_toastMutex);
    g_toasts.erase(std::remove_if(g_toasts.begin(), g_toasts.end(), [](const Toast& t) { return t.age > t.seconds + 0.4f; }), g_toasts.end());
    if (g_toasts.empty())
        return;

    const float scale = Ui::Scale;
    const ImGuiViewport* viewport = ImGui::GetMainViewport();
    const float margin = 20.0f * scale;
    const float gap = 10.0f * scale;
    const float width = 320.0f * scale;
    float offset = 0.0f;

    for (int i = static_cast<int>(g_toasts.size()) - 1; i >= 0; --i)
    {
        Toast& toast = g_toasts[i];
        toast.age += Anim::Delta();
        const float appear = Anim::EaseOutCubic(toast.age / 0.28f);
        const float leave = Anim::EaseOutCubic((toast.seconds + 0.4f - toast.age) / 0.4f);
        const float alpha = std::min(appear, leave);
        const float stack = Anim::To(0xB1A27000u + static_cast<ImGuiID>(toast.id), offset, 14.0f, offset);
        const float slide = (1.0f - alpha) * 28.0f * scale;
        const bool right = Corner == ToastCorner::TopRight || Corner == ToastCorner::BottomRight;
        const bool top = Corner == ToastCorner::TopRight || Corner == ToastCorner::TopLeft;
        const float x = right ? viewport->WorkPos.x + viewport->WorkSize.x - margin + slide : viewport->WorkPos.x + margin - slide;
        const float y = top ? viewport->WorkPos.y + margin + stack : viewport->WorkPos.y + viewport->WorkSize.y - margin - stack;

        ImGui::SetNextWindowPos(ImVec2(x, y), ImGuiCond_Always, ImVec2(right ? 1.0f : 0.0f, top ? 0.0f : 1.0f));
        ImGui::SetNextWindowSize(ImVec2(width, 0.0f));
        ImGui::PushStyleVar(ImGuiStyleVar_Alpha, alpha);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(14.0f * scale, 12.0f * scale));
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 6.0f * scale);
        ImGui::PushStyleColor(ImGuiCol_WindowBg, P().Elevated);

        char name[32];
        snprintf(name, sizeof(name), "##toast%d", toast.id);
        ImGui::Begin(name, nullptr, ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoNav |
            ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_AlwaysAutoResize);
        Ui::Text(toast.icon.c_str(), toast.tone);
        ImGui::SameLine(0.0f, 10.0f * scale);
        ImGui::BeginGroup();
        Fonts::Push(Fonts::SemiBold);
        ImGui::TextUnformatted(toast.title.c_str());
        Fonts::Pop();
        if (!toast.message.empty())
        {
            ImGui::PushStyleColor(ImGuiCol_Text, P().TextMuted);
            ImGui::PushTextWrapPos(ImGui::GetCursorPosX() + width - 60.0f * scale);
            ImGui::TextUnformatted(toast.message.c_str());
            ImGui::PopTextWrapPos();
            ImGui::PopStyleColor();
        }
        ImGui::EndGroup();
        toast.height = ImGui::GetWindowHeight();
        ImGui::End();
        ImGui::PopStyleColor();
        ImGui::PopStyleVar(3);

        offset += (toast.height + gap) * std::min(1.0f, alpha * 1.5f);
    }
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// menu
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

Blaze::MenuTab& Blaze::MenuPage::AddTab(const char* title, std::function<void()> draw, const char* about)
{
    MenuTab tab;
    tab.Title = title;
    tab.Draw = std::move(draw);
    tab.About = about ? about : "";
    tabs.push_back(std::move(tab));
    return tabs.back();
}

void Blaze::MenuPage::SetSelectedTab(int index)
{
    index = std::clamp(index, 0, std::max(0, static_cast<int>(tabs.size()) - 1));
    if (index == selectedTab)
        return;
    selectedTab = index;
    tabChanged = true;
}

void Blaze::MenuPage::SelectTab(const char* title)
{
    for (int i = 0; i < static_cast<int>(tabs.size()); ++i)
        if (tabs[i].Title == title)
            return SetSelectedTab(i);
}

Blaze::MenuPage& Blaze::Menu::AddPage(const char* title, const char* icon, const char* caption)
{
    MenuPage page;
    page.Title = title;
    page.Icon = icon ? icon : "";
    page.Caption = caption ? caption : "";
    pages.push_back(std::move(page));
    return pages.back();
}

Blaze::MenuPage& Blaze::Menu::AddPage(const char* title, const char* icon, std::function<void()> draw, const char* caption)
{
    MenuPage& page = AddPage(title, icon, caption);
    page.AddTab(title, std::move(draw));
    return page;
}

void Blaze::Menu::SetSelected(int index)
{
    index = std::clamp(index, 0, std::max(0, static_cast<int>(pages.size()) - 1));
    if (index == selected)
        return;
    selected = index;
    pageProgress = 0.0f;
    navProgress = 0.0f;
    ResetAbout();
}

void Blaze::Menu::Select(const char* title)
{
    const char* slash = std::strchr(title, '/');
    const std::string pageTitle = slash ? std::string(title, slash) : std::string(title);
    for (int i = 0; i < static_cast<int>(pages.size()); ++i)
        if (pages[i].Title == pageTitle)
        {
            SetSelected(i);
            if (slash)
                pages[i].SelectTab(slash + 1);
            return;
        }
}

Blaze::MenuTab* Blaze::Menu::CurrentTab()
{
    MenuPage* page = Page(selected);
    return page && !page->Tabs().empty() ? &page->tabs[page->SelectedTab()] : nullptr;
}

void Blaze::Menu::ResetAbout()
{
    const MenuTab* tab = CurrentTab();
    Ui::CurrentAbout = tab ? tab->About : std::string();
}

namespace
{
    float MenuPad() { return 18.0f * Blaze::Ui::Scale; }
    float MenuTitleY() { return 17.0f * Blaze::Ui::Scale; }
    float MenuBodyTop() { return 54.0f * Blaze::Ui::Scale; }

    // A soft shadow under the window, drawn behind every ImGui window.
    void DrawMenuShadow(const ImVec2& origin, const ImVec2& size)
    {
        ImDrawList* draw = ImGui::GetBackgroundDrawList();
        const float scale = Blaze::Ui::Scale;
        const float rounding = ImGui::GetStyle().WindowRounding;
        const float strength = Blaze::Theme::IsDark() ? 0.09f : 0.028f;
        for (int i = 1; i <= 10; ++i)
        {
            const float grow = static_cast<float>(i) * 2.4f * scale;
            const ImVec2 offset(0.0f, 6.0f * scale);
            draw->AddRectFilled(origin - ImVec2(grow, grow) + offset, origin + size + ImVec2(grow, grow) + offset,
                Blaze::Theme::U32(ImVec4(0, 0, 0, strength * (1.0f - static_cast<float>(i) / 11.0f))), rounding + grow);
        }
    }
}

void Blaze::Menu::Draw(float visibility, bool interactive)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    const ImGuiViewport* viewport = ImGui::GetMainViewport();

    // Never larger than the screen (games run at any resolution).
    const ImVec2 screen = viewport->WorkSize;
    const ImVec2 minSize(std::min(MinSize.x * scale, screen.x), std::min(MinSize.y * scale, screen.y));
    const ImVec2 maxSize(std::max(minSize.x, screen.x), std::max(minSize.y, screen.y));
    ImGui::SetNextWindowSize(ImVec2(std::min(Size.x * scale, screen.x * 0.98f), std::min(Size.y * scale, screen.y * 0.98f)), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(minSize, maxSize);

    // The window rises slightly while it fades in; once open the user can drag it freely.
    const float rise = (1.0f - Anim::EaseOutCubic(visibility)) * 14.0f * scale;
    if (centerPending)
    {
        ImGui::SetNextWindowPos(viewport->WorkPos + viewport->WorkSize * 0.5f, ImGuiCond_Always, ImVec2(0.5f, 0.5f));
        restPosition.reset();
        centerPending = false;
    }
    else if (restPosition && visibility < 1.0f)
        ImGui::SetNextWindowPos(*restPosition + ImVec2(0.0f, rise), ImGuiCond_Always);
    else if (!restPosition)
        ImGui::SetNextWindowPos(viewport->WorkPos + viewport->WorkSize * 0.5f, ImGuiCond_FirstUseEver, ImVec2(0.5f, 0.5f));

    ImGuiWindowFlags flags = ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoScrollWithMouse;
    if (!Resizable)
        flags |= ImGuiWindowFlags_NoResize;
    if (!interactive)
        flags |= ImGuiWindowFlags_NoInputs;

    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0.0f, 0.0f));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 1.0f);
    const std::string name = Title + "##BlazeMenu";
    ImGui::Begin(name.c_str(), nullptr, flags);
    ImGui::PopStyleVar(2);

    if (visibility >= 1.0f || !restPosition)
        restPosition = ImGui::GetWindowPos();

    const ImVec2 origin = ImGui::GetWindowPos();
    const ImVec2 size = ImGui::GetWindowSize();
    windowCenter = origin + size * 0.5f;
    DrawMenuShadow(origin, size);

    // A tab switched from code (or a click) since the last frame replays the content transition.
    if (MenuPage* page = Page(selected); page && page->tabChanged)
    {
        page->tabChanged = false;
        pageProgress = 0.0f;
        ResetAbout();
    }
    pageProgress = std::min(1.0f, pageProgress + Anim::Delta() / 0.26f);
    navProgress = std::min(1.0f, navProgress + Anim::Delta() / 0.26f);

    const float rail = RailWidth * scale;
    const float nav = NavWidth * scale;
    const float content = ShowStatus ? ContentWidth * scale : size.x - rail - nav;
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float lines[] = { rail, rail + nav, rail + nav + content };
    for (int i = 0; i < (ShowStatus ? 3 : 2); ++i)
        draw->AddLine(origin + ImVec2(lines[i], 0.0f), origin + ImVec2(lines[i], size.y), Theme::U32(p.Border));

    DrawRail(rail, size.y);
    DrawNav(rail, nav, size.y);
    Ui::InlineDescriptions = !ShowStatus;
    if (Ui::CurrentAbout.empty())
        ResetAbout();
    DrawContent(ImVec2(rail + nav + 1.0f, 0.0f), ImVec2(content - 1.0f, size.y));
    if (ShowStatus)
        DrawStatus(ImVec2(rail + nav + content + 1.0f, 0.0f), ImVec2(size.x - rail - nav - content - 1.0f, size.y));
    ImGui::End();
}

void Blaze::Menu::DrawRail(float width, float height)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 origin = ImGui::GetWindowPos();

    // brand mark: a bold letter (or icon) with the brand dot at its baseline
    const std::string mark = !Logo.empty() ? Logo : Title.empty() ? std::string() : std::string(1, static_cast<char>(std::toupper(static_cast<unsigned char>(Title[0]))));
    const ImVec2 markSize = Fonts::Brand.CalcTextSize(mark.c_str());
    const float dot = 2.6f * scale;
    const ImVec2 markPos = origin + ImVec2((width - markSize.x - dot * 2.4f) * 0.5f, 29.0f * scale - markSize.y * 0.5f);
    draw->AddText(Fonts::Brand.font, Fonts::Brand.Px(), markPos, Theme::U32(p.Text), mark.c_str());
    draw->AddCircleFilled(markPos + ImVec2(markSize.x + dot * 1.3f, markSize.y * 0.78f), dot, Theme::U32(p.Brand));

    // Icons: top-aligned pages from the top, pinned pages from the bottom. The tile glides beneath them.
    const float tile = 40.0f * scale;
    const float pitch = 52.0f * scale;
    const float top = 99.0f * scale;
    const float bottom = height - 40.0f * scale;
    int tops = 0;
    int bottoms = static_cast<int>(std::count_if(pages.begin(), pages.end(), [](const MenuPage& page) { return page.PinBottom; }));
    float selectedY = 0.0f;
    draw->ChannelsSplit(2);
    draw->ChannelsSetCurrent(1);

    for (int i = 0; i < static_cast<int>(pages.size()); ++i)
    {
        const MenuPage& page = pages[i];
        const float cy = page.PinBottom ? bottom - static_cast<float>(--bottoms) * pitch : top + static_cast<float>(tops++) * pitch;
        const ImVec2 min((width - tile) * 0.5f, cy - tile * 0.5f);
        ImGui::SetCursorPos(min);
        ImGui::PushID(i);
        if (ImGui::InvisibleButton("##rail", ImVec2(tile, tile)))
            SetSelected(i);
        const bool hovered = ImGui::IsItemHovered();
        if (hovered)
            ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
        const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered && i != selected, 18.0f);
        const float active = Anim::Toggle(ImGui::GetID("##active"), i == selected, 16.0f);
        ImGui::PopID();
        Ui::Tooltip(page.Title.c_str());

        const ImVec2 center = origin + min + ImVec2(tile, tile) * 0.5f;
        const ImVec4 color = Anim::Lerp(Anim::Lerp(p.TextMuted, p.Text, hover), p.OnAccent, active);
        const ImVec2 glyph = Fonts::IconLarge.CalcTextSize(page.Icon.c_str());
        draw->AddText(Fonts::IconLarge.font, Fonts::IconLarge.Px(), center - glyph * 0.5f, Theme::U32(color), page.Icon.c_str());
        if (page.Badge)
            draw->AddCircleFilled(center + ImVec2(tile * 0.3f, -tile * 0.3f), 3.0f * scale, Theme::U32(p.Brand));
        if (i == selected)
            selectedY = min.y;
    }

    draw->ChannelsSetCurrent(0);
    if (!pages.empty())
    {
        const float y = Anim::To(ImGui::GetID("##railtile"), selectedY, 18.0f);
        const ImVec2 min = origin + ImVec2((width - tile) * 0.5f, y);
        draw->AddRectFilled(min, min + ImVec2(tile, tile), Theme::U32(p.Accent), 9.0f * scale);
    }
    draw->ChannelsMerge();
}

void Blaze::Menu::DrawNav(float x, float width, float height)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 origin = ImGui::GetWindowPos() + ImVec2(x, 0.0f);
    MenuPage* page = Page(selected);
    const float t = Anim::EaseOutCubic(navProgress);
    const float slide = (1.0f - t) * 6.0f * scale;
    const float pad = MenuPad();

    if (page)
    {
        draw->AddText(Fonts::Title.font, Fonts::Title.Px(), origin + ImVec2(pad + slide, MenuTitleY()), Theme::U32(p.Text, t), page->Title.c_str());
        if (!page->Caption.empty())
        {
            std::string caption = page->Caption;
            std::transform(caption.begin(), caption.end(), caption.begin(), [](unsigned char c) { return static_cast<char>(std::toupper(c)); });
            draw->AddText(Fonts::Small.font, Fonts::Small.Px(), origin + ImVec2(pad + slide, 59.0f * scale), Theme::U32(p.TextFaint, t), caption.c_str());
        }

        // Tabs. The selected one is in full text color with a short marker at the column's right edge.
        float y = 102.0f * scale;
        const float row = 29.0f * scale;
        float selectedY = y;
        for (int i = 0; i < static_cast<int>(page->Tabs().size()); ++i)
        {
            const MenuTab& tab = page->Tabs()[i];
            ImGui::SetCursorPos(ImVec2(x + pad * 0.5f, y));
            ImGui::PushID(selected * 1000 + i);
            if (ImGui::InvisibleButton("##tab", ImVec2(width - pad, row)))
                page->SetSelectedTab(i);
            const bool hovered = ImGui::IsItemHovered();
            if (hovered)
                ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
            const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered, 18.0f);
            const float active = Anim::Toggle(ImGui::GetID("##active"), i == page->SelectedTab(), 16.0f);
            ImGui::PopID();
            const ImVec4 color = Anim::Lerp(Anim::Lerp(p.TextFaint, p.TextMuted, hover), p.Text, active);
            const float textY = y + (row - ImGui::GetTextLineHeight()) * 0.5f;
            draw->AddText(origin + ImVec2(pad + slide, textY), Theme::U32(color, t), tab.Title.c_str());
            if (i == page->SelectedTab())
                selectedY = y;
            y += row;
        }
        if (!page->Tabs().empty())
        {
            // Keyed per page so switching pages snaps the marker instead of gliding across pages.
            char key[32];
            snprintf(key, sizeof(key), "##tabmarker%d", selected);
            const float markerY = Anim::To(ImGui::GetID(key), selectedY, 18.0f);
            const float markerHeight = 11.0f * scale;
            const float mx = origin.x + width - 15.0f * scale;
            const float my = origin.y + markerY + (row - markerHeight) * 0.5f;
            draw->AddRectFilled(ImVec2(mx, my), ImVec2(mx + 2.0f * scale, my + markerHeight), Theme::U32(p.Text, t), scale);
        }
    }

    // footer: brand name, subtitle and version on one baseline, level with the bottom rail icon
    const float titleTop = height - 40.0f * scale - Fonts::Title.Px() * 0.5f;
    const ImVec2 footer = origin + ImVec2(pad, titleTop);
    draw->AddText(Fonts::Title.font, Fonts::Title.Px(), footer, Theme::U32(p.Text), Title.c_str());
    float cursor = footer.x + Fonts::Title.CalcTextSize(Title.c_str()).x + 8.0f * scale;
    const float smallY = footer.y + Fonts::Title.Px() - Fonts::Small.Px() - 1.0f * scale;
    if (!Subtitle.empty())
    {
        draw->AddText(Fonts::Small.font, Fonts::Small.Px(), ImVec2(cursor, smallY), Theme::U32(p.TextMuted), Subtitle.c_str());
        cursor += Fonts::Small.CalcTextSize(Subtitle.c_str()).x + 6.0f * scale;
    }
    if (!Version.empty())
        draw->AddText(Fonts::SmallSemiBold.font, Fonts::SmallSemiBold.Px(), ImVec2(cursor, smallY), Theme::U32(p.Text), Version.c_str());
}

void Blaze::Menu::DrawContent(const ImVec2& position, const ImVec2& size)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 origin = ImGui::GetWindowPos() + position;
    MenuPage* page = Page(selected);
    MenuTab* tab = CurrentTab();
    const float t = Anim::EaseOutCubic(pageProgress);
    const float pad = MenuPad();
    if (tab)
        draw->AddText(Fonts::Title.font, Fonts::Title.Px(), origin + ImVec2(pad + (1.0f - t) * 6.0f * scale, MenuTitleY()), Theme::U32(p.Text, t),
            tab->Title.c_str());

    // Body: fades and rises in after a switch. Each tab keeps its own scroll position.
    ImGui::SetCursorPos(position + ImVec2(0.0f, MenuBodyTop()));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(pad, 6.0f * scale));
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, ImGui::GetStyle().Alpha * t);
    ImGuiWindowFlags bodyFlags = ImGuiWindowFlags_NoBackground;
    if (page && !page->Scroll)
        bodyFlags |= ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse;
    char id[48];
    snprintf(id, sizeof(id), "##content%d.%d", selected, page ? page->SelectedTab() : 0);
    ImGui::BeginChild(id, ImVec2(size.x, size.y - MenuBodyTop() - 4.0f * scale), ImGuiChildFlags_AlwaysUseWindowPadding, bodyFlags);
    ImGui::PopStyleVar(2);
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, ImGui::GetStyle().Alpha * t);
    if (t < 1.0f)
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() + (1.0f - t) * 10.0f * scale);
    if (tab && tab->Draw)
        tab->Draw();
    if (!page || page->Scroll)
        ImGui::Dummy(ImVec2(0.0f, 6.0f * scale));
    ImGui::PopStyleVar();
    ImGui::EndChild();
}

void Blaze::Menu::DrawStatus(const ImVec2& position, const ImVec2& size)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 origin = ImGui::GetWindowPos() + position;
    const float pad = MenuPad();
    draw->AddText(Fonts::Title.font, Fonts::Title.Px(), origin + ImVec2(pad, MenuTitleY()), Theme::U32(p.Text), StatusTitle.c_str());

    // live readouts above, the ABOUT hint for the hovered control anchored near the bottom
    const float aboutTop = size.y - 168.0f * scale;
    ImGui::SetCursorPos(position + ImVec2(0.0f, MenuBodyTop()));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(pad, 6.0f * scale));
    ImGui::BeginChild("##status", ImVec2(size.x, aboutTop - MenuBodyTop() - 8.0f * scale), ImGuiChildFlags_AlwaysUseWindowPadding,
        ImGuiWindowFlags_NoBackground);
    ImGui::PopStyleVar();
    const MenuPage* page = Page(selected);
    if (page && page->Status)
        page->Status();
    else if (Status)
        Status();
    ImGui::EndChild();

    if (Ui::CurrentAbout.empty())
        return;
    ImGui::SetCursorPos(position + ImVec2(pad, aboutTop));
    ImGui::BeginGroup();
    ImGui::PushTextWrapPos(ImGui::GetCursorPosX() + size.x - pad * 2.5f);
    Ui::Caption(AboutTitle.c_str());
    Fonts::Push(Fonts::Small);
    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(0.0f, 0.0f));
    Ui::Text(Ui::CurrentAbout.c_str(), p.TextMuted);
    ImGui::PopStyleVar();
    Fonts::Pop();
    ImGui::PopTextWrapPos();
    ImGui::EndGroup();
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// overlay
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

Blaze::Overlay::Overlay(const OverlayOptions& options) : options(options), menuVisible(options.StartVisible)
{
    menu.Title = options.Title;
    menu.Subtitle = options.Subtitle;
    menu.Version = options.Version;
    menu.Logo = options.Logo;
    menu.Size = options.MenuSize;
    menu.OnClose = [this] { menuVisible = false; };
}

void Blaze::Overlay::Setup()
{
    Ui::Scale = options.UiScale;
    Fonts::Load(options.FontSize, options.MergeFonts);
    Theme::Set(options.Theme, false);
    if (options.Accent)
        Theme::SetAccent(*options.Accent, false);
    ImGui::GetStyle().FontScaleMain = options.UiScale;
    Theme::ApplyStyle(options.UiScale);
}

void Blaze::Overlay::SetScale(float scale)
{
    if (std::fabs(scale - Ui::Scale) < 0.001f)
        return;

    Ui::Scale = scale;
    ImGui::GetStyle().FontScaleMain = scale;
    Theme::ApplyStyle(scale);
}

void Blaze::Overlay::Render()
{
    Input::Update();
    Theme::Update();

    menuAlpha = Anim::To(0xB1A2E001u, menuVisible ? 1.0f : 0.0f, menuVisible ? 14.0f : 20.0f, 0.0f);
    if (menuAlpha > 0.002f)
    {
        ImGui::PushStyleVar(ImGuiStyleVar_Alpha, menuAlpha);
        menu.Draw(menuAlpha, menuVisible);
        ImGui::PopStyleVar();
    }

    Notify::Draw();
}
