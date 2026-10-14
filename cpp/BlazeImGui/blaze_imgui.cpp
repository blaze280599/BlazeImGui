#ifndef IMGUI_DEFINE_MATH_OPERATORS
#define IMGUI_DEFINE_MATH_OPERATORS
#endif
#include "blaze_imgui.h"

#include <algorithm>
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
        p.Knob = L(a.Knob, b.Knob);
        p.Success = L(a.Success, b.Success); p.Warning = L(a.Warning, b.Warning); p.Danger = L(a.Danger, b.Danger);
        return p;
    }

    Palette Palette::DefaultDark()
    {
        Palette p;
        p.Background = Hex("#111216");
        p.Sidebar = Hex("#0C0D10");
        p.Surface = Hex("#17181D");
        p.Elevated = Hex("#1B1C22");
        p.Field = Hex("#22242B");
        p.FieldHover = Hex("#2A2C34");
        p.FieldActive = Hex("#31343D");
        p.Raised = Hex("#383B45");
        p.Border = Hex("#FFFFFF", 0.07f);
        p.Text = Hex("#ECEDF0");
        p.TextMuted = Hex("#959AA4");
        p.TextFaint = Hex("#62666F");
        p.Accent = Hex("#6D8CFF");
        p.AccentHover = Hex("#8199FF");
        p.OnAccent = Hex("#FFFFFF");
        p.Knob = Hex("#FFFFFF");
        p.Success = Hex("#3DD68C");
        p.Warning = Hex("#F5B74F");
        p.Danger = Hex("#FF6B6B");
        return p;
    }

    Palette Palette::DefaultLight()
    {
        Palette p;
        p.Background = Hex("#FFFFFF");
        p.Sidebar = Hex("#F6F7F9");
        p.Surface = Hex("#F7F8FA");
        p.Elevated = Hex("#FFFFFF");
        p.Field = Hex("#EDEFF3");
        p.FieldHover = Hex("#E4E7EC");
        p.FieldActive = Hex("#DADEE5");
        p.Raised = Hex("#FFFFFF");
        p.Border = Hex("#0B0D12", 0.09f);
        p.Text = Hex("#15171C");
        p.TextMuted = Hex("#646A75");
        p.TextFaint = Hex("#9AA0AA");
        p.Accent = Hex("#4F6EF7");
        p.AccentHover = Hex("#4262EE");
        p.OnAccent = Hex("#FFFFFF");
        p.Knob = Hex("#FFFFFF");
        p.Success = Hex("#16A36A");
        p.Warning = Hex("#D9891A");
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
        Blaze::Palette from = dark;
        Blaze::Palette colors = dark;
        float progress = 1.0f;
        Blaze::ThemeMode mode = Blaze::ThemeMode::Dark;
        float rounding = 8.0f;
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
        c[ImGuiCol_FrameBg] = p.Field;
        c[ImGuiCol_FrameBgHovered] = p.FieldHover;
        c[ImGuiCol_FrameBgActive] = p.FieldActive;
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
        c[ImGuiCol_Header] = A(p.Accent, 0.14f);
        c[ImGuiCol_HeaderHovered] = p.FieldHover;
        c[ImGuiCol_HeaderActive] = A(p.Accent, 0.22f);
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

    style.WindowRounding = 12;
    style.ChildRounding = 10;
    style.FrameRounding = 7;
    style.PopupRounding = 9;
    style.GrabRounding = 7;
    style.TabRounding = 7;
    style.ScrollbarRounding = 9;
    style.WindowBorderSize = 1;
    style.ChildBorderSize = 1;
    style.PopupBorderSize = 1;
    style.FrameBorderSize = 0;
    style.TabBorderSize = 0;
    style.TabBarBorderSize = 1;
    style.TabBarOverlineSize = 2;
    style.WindowPadding = ImVec2(16, 14);
    style.FramePadding = ImVec2(10, 6);
    style.ItemSpacing = ImVec2(10, 8);
    style.ItemInnerSpacing = ImVec2(8, 6);
    style.CellPadding = ImVec2(10, 6);
    style.IndentSpacing = 18;
    style.ScrollbarSize = 10;
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

    float SmallLineHeight()
    {
        Blaze::Fonts::Push(Blaze::Fonts::Small);
        const float height = ImGui::GetTextLineHeight();
        Blaze::Fonts::Pop();
        return height;
    }

    void Description(const char* description)
    {
        if (!description)
            return;

        Blaze::Fonts::Push(Blaze::Fonts::Small);
        ImGui::PushStyleColor(ImGuiCol_Text, P().TextMuted);
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() - S(4));
        Blaze::Ui::TextWrapped(description);
        ImGui::PopStyleColor();
        Blaze::Fonts::Pop();
    }
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

void Blaze::Ui::Section(const char* title)
{
    if (ImGui::GetCursorPosY() > ImGui::GetStyle().WindowPadding.y + 1.0f)
        ImGui::Dummy(ImVec2(0.0f, S(6)));

    Fonts::Push(Fonts::SmallSemiBold);
    Text(Visible(title).c_str(), P().TextMuted);
    Fonts::Pop();
    ImGui::Dummy(ImVec2(0.0f, S(2)));
}

void Blaze::Ui::BeginCard(const char* id, const char* title, const char* icon, float width)
{
    ImGui::PushStyleColor(ImGuiCol_ChildBg, P().Surface);
    ImGui::PushStyleColor(ImGuiCol_Border, P().Border);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(16, 14));
    ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, S(10));
    ImGui::BeginChild(id, ImVec2(width, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysUseWindowPadding,
        ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleVar(2);
    ImGui::PopStyleColor(2);

    if (!title)
        return;

    Fonts::Push(Fonts::SemiBold);
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
    draw->AddRectFilled(start, start + ImVec2(width, height), Col(P().Surface), S(10));
    draw->AddRect(start, start + ImVec2(width, height), Col(P().Border), S(10));

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

bool Blaze::Ui::Toggle(const char* label, bool* value, const char* description)
{
    ImGui::PushID(label);
    const std::string text = Visible(label);
    const float width = std::max(1.0f, ImGui::GetContentRegionAvail().x);
    const ImVec2 start = ImGui::GetCursorScreenPos();
    const float lineHeight = ImGui::GetTextLineHeight();
    const float descriptionHeight = description ? SmallLineHeight() + S(2) : 0.0f;
    const float height = std::max(ImGui::GetFrameHeight(), lineHeight + descriptionHeight + S(6));

    const bool changed = ImGui::InvisibleButton("##row", ImVec2(width, height));
    if (changed)
        *value = !*value;
    const bool hovered = ImGui::IsItemHovered();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
    const float on = Anim::Toggle(ImGui::GetID("##on"), *value, 16.0f);
    const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered, 18.0f);

    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 size = S(34, 20);
    const ImVec2 track(start.x + width - size.x, start.y + (height - size.y) * 0.5f);
    const ImVec4 off = Anim::Lerp(P().Field, P().FieldActive, hover);
    const ImVec4 onColor = Anim::Lerp(P().Accent, P().AccentHover, hover);
    draw->AddRectFilled(track, track + size, Col(Anim::Lerp(off, onColor, on)), size.y * 0.5f);
    const float radius = size.y * 0.5f - S(3);
    const ImVec2 knob(Anim::Lerp(track.x + S(3) + radius, track.x + size.x - S(3) - radius, on), track.y + size.y * 0.5f);
    draw->AddCircleFilled(knob + ImVec2(0.0f, S(0.75f)), radius + S(0.5f), Col(ImVec4(0, 0, 0, Theme::IsDark() ? 0.35f : 0.18f)));
    draw->AddCircleFilled(knob, radius, Col(P().Knob));

    const float textY = start.y + (height - lineHeight - descriptionHeight) * 0.5f;
    draw->AddText(ImVec2(start.x, textY), Col(P().Text), text.c_str());
    if (description)
        draw->AddText(Fonts::Small.font, Fonts::Small.Px(), ImVec2(start.x, textY + lineHeight + S(2)), Col(P().TextMuted), description);

    ImGui::PopID();
    return changed;
}

bool Blaze::Ui::Checkbox(const char* label, bool* value)
{
    ImGui::PushID(label);
    const std::string text = Visible(label);
    const float box = ImGui::GetFrameHeight() - S(8);
    const ImVec2 start = ImGui::GetCursorScreenPos();
    const ImVec2 textSize = ImGui::CalcTextSize(text.c_str());
    const float height = ImGui::GetFrameHeight();
    const float width = box + (!text.empty() ? ImGui::GetStyle().ItemInnerSpacing.x + textSize.x : 0.0f);

    const bool changed = ImGui::InvisibleButton("##box", ImVec2(width, height));
    if (changed)
        *value = !*value;
    const bool hovered = ImGui::IsItemHovered();
    const float on = Anim::Toggle(ImGui::GetID("##on"), *value, 20.0f);
    const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered, 18.0f);

    ImDrawList* draw = ImGui::GetWindowDrawList();
    const ImVec2 min(start.x, start.y + (height - box) * 0.5f);
    const ImVec4 rest = Anim::Lerp(P().Field, P().FieldHover, hover);
    draw->AddRectFilled(min, min + ImVec2(box, box), Col(Anim::Lerp(rest, P().Accent, on)), S(5));

    if (on > 0.01f)
    {
        // a drawn check that grows in with the fill
        const ImVec2 a = min + ImVec2(box * 0.24f, box * 0.52f);
        const ImVec2 b = min + ImVec2(box * 0.42f, box * 0.70f);
        const ImVec2 c = min + ImVec2(box * 0.76f, box * 0.32f);
        const float t = Anim::EaseOutCubic(on);
        draw->AddLine(a, Anim::Lerp(a, b, std::min(1.0f, t * 2.0f)), Col(P().OnAccent, on), S(1.8f));
        if (t > 0.5f)
            draw->AddLine(b, Anim::Lerp(b, c, (t - 0.5f) * 2.0f), Col(P().OnAccent, on), S(1.8f));
    }

    if (!text.empty())
        draw->AddText(ImVec2(min.x + box + ImGui::GetStyle().ItemInnerSpacing.x, start.y + (height - textSize.y) * 0.5f), Col(P().Text), text.c_str());

    ImGui::PopID();
    return changed;
}

namespace
{
    bool SliderCore(const char* label, float* value, float min, float max, float step, const char* shown, const char* description)
    {
        using namespace Blaze;
        ImGui::PushID(label);
        const float width = std::max(1.0f, ImGui::GetContentRegionAvail().x);
        const float x0 = ImGui::GetCursorPosX();
        ImGui::TextUnformatted(Ui::Visible(label).c_str());
        ImGui::SameLine(x0 + width - ImGui::CalcTextSize(shown).x);
        Ui::Text(shown, P().TextMuted);

        const float height = S(18);
        const ImVec2 start = ImGui::GetCursorScreenPos();
        ImGui::InvisibleButton("##track", ImVec2(width, height));
        const bool active = ImGui::IsItemActive();
        const bool hovered = ImGui::IsItemHovered();
        if (hovered || active)
            ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

        const float radius = S(7);
        bool changed = false;
        if (active && max > min)
        {
            const float fraction = std::clamp((ImGui::GetIO().MousePos.x - start.x - radius) / (width - radius * 2.0f), 0.0f, 1.0f);
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
        const float grow = Anim::Toggle(ImGui::GetID("##grow"), hovered || active, 18.0f);

        ImDrawList* draw = ImGui::GetWindowDrawList();
        const float cy = start.y + height * 0.5f;
        const float left = start.x + radius;
        const float right = start.x + width - radius;
        const float knobX = Anim::Lerp(left, right, t);
        const float thick = S(4);
        draw->AddRectFilled(ImVec2(left - thick * 0.5f, cy - thick * 0.5f), ImVec2(right + thick * 0.5f, cy + thick * 0.5f), Col(P().Field), thick);
        draw->AddRectFilled(ImVec2(left - thick * 0.5f, cy - thick * 0.5f), ImVec2(knobX, cy + thick * 0.5f), Col(P().Accent), thick);
        const float r = Anim::Lerp(S(6), radius, grow);
        draw->AddCircleFilled(ImVec2(knobX, cy + S(0.75f)), r + S(0.5f), Col(ImVec4(0, 0, 0, Theme::IsDark() ? 0.4f : 0.18f)));
        draw->AddCircleFilled(ImVec2(knobX, cy), r, Col(P().Knob));
        Description(description);
        ImGui::PopID();
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
        size.x = textSize.x + padding.x * 2.0f + S(6);
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

    ImVec4 background, foreground;
    switch (kind)
    {
    case ButtonKind::Primary:
        background = Palette::Shade(Anim::Lerp(P().Accent, P().AccentHover, hover), -0.08f * press);
        foreground = P().OnAccent;
        break;
    case ButtonKind::Ghost:
        background = Anim::Lerp(Palette::Alpha(P().FieldHover, 0.0f), P().FieldHover, std::max(hover, press));
        foreground = Anim::Lerp(P().TextMuted, P().Text, hover);
        break;
    case ButtonKind::Danger:
        background = Palette::Alpha(P().Danger, Anim::Lerp(0.12f, 0.2f, hover) + 0.06f * press);
        foreground = P().Danger;
        break;
    default:
        background = Anim::Lerp(Anim::Lerp(P().Field, P().FieldHover, hover), P().FieldActive, press);
        foreground = P().Text;
        break;
    }

    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(start, start + size, Col(background), ImGui::GetStyle().FrameRounding);
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
    draw->AddRectFilled(start, start + ImVec2(size, size), Col(P().FieldHover, hover), ImGui::GetStyle().FrameRounding);
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
        width = std::max(1.0f, ImGui::GetContentRegionAvail().x);

    const float height = ImGui::GetFrameHeight();
    const ImVec2 start = ImGui::GetCursorScreenPos();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float rounding = ImGui::GetStyle().FrameRounding;
    const float inset = S(3);
    const float segment = (width - inset * 2.0f) / static_cast<float>(count);
    draw->AddRectFilled(start, start + ImVec2(width, height), Col(P().Field), rounding);

    const float at = Anim::To(ImGui::GetID("##thumb"), static_cast<float>(*index), 16.0f);
    const ImVec2 thumbMin(start.x + inset + segment * at, start.y + inset);
    const ImVec2 thumbMax = thumbMin + ImVec2(segment, height - inset * 2.0f);
    if (!Theme::IsDark())
        draw->AddRectFilled(thumbMin + ImVec2(0.0f, S(1)), thumbMax + ImVec2(0.0f, S(1)), Col(ImVec4(0, 0, 0, 0.06f)), rounding - inset * 0.5f);
    draw->AddRectFilled(thumbMin, thumbMax, Col(P().Raised), rounding - inset * 0.5f);

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
        const ImVec4 color = Anim::Lerp(Anim::Lerp(P().TextMuted, P().Text, hover * 0.6f), P().Text, selected);
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
    return std::min(S(210), ImGui::GetContentRegionAvail().x * 0.5f);
}

void Blaze::Ui::Row(const char* label, float controlWidth)
{
    if (controlWidth <= 0.0f)
        controlWidth = ControlWidth();

    const float x0 = ImGui::GetCursorPosX();
    const float width = ImGui::GetContentRegionAvail().x;
    ImGui::AlignTextToFramePadding();
    ImGui::TextUnformatted(Visible(label).c_str());
    ImGui::SameLine(x0 + width - controlWidth);
    ImGui::SetNextItemWidth(controlWidth);
}

bool Blaze::Ui::Combo(const char* label, int* index, const char* const* items, int count, const char* description)
{
    Row(label);
    // The frame is measured before BeginCombo, which moves into the popup window when open.
    const ImVec2 min = ImGui::GetCursorScreenPos();
    const ImVec2 max = min + ImVec2(ImGui::CalcItemWidth(), ImGui::GetFrameHeight());
    ImDrawList* draw = ImGui::GetWindowDrawList();
    bool changed = false;
    const char* preview = *index >= 0 && *index < count ? items[*index] : "";
    const std::string id = std::string("##") + label;

    const bool open = ImGui::BeginCombo(id.c_str(), preview, ImGuiComboFlags_NoArrowButton);
    const char* chevron = open ? ICON_LC_CHEVRON_UP : ICON_LC_CHEVRON_DOWN;
    const ImVec2 size = ImGui::CalcTextSize(chevron);
    draw->AddText(ImVec2(max.x - size.x - ImGui::GetStyle().FramePadding.x, min.y + (max.y - min.y - size.y) * 0.5f), Col(P().TextMuted), chevron);

    if (open)
    {
        for (int i = 0; i < count; ++i)
        {
            if (ImGui::Selectable(items[i], i == *index))
            {
                *index = i;
                changed = true;
            }
            if (i == *index)
                ImGui::SetItemDefaultFocus();
        }
        ImGui::EndCombo();
    }

    Description(description);
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
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::InputTextWithHint(id.c_str(), hint, value->data(), value->capacity() + 1, ImGuiInputTextFlags_CallbackResize,
        ResizeString, value);
    Description(description);
    return changed;
}

bool Blaze::Ui::Number(const char* label, float* value, float speed, float min, float max, const char* format, const char* description)
{
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::DragFloat(id.c_str(), value, speed, min, max, format);
    Description(description);
    return changed;
}

bool Blaze::Ui::Number(const char* label, int* value, float speed, int min, int max, const char* description)
{
    Row(label);
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::DragInt(id.c_str(), value, speed, min, max);
    Description(description);
    return changed;
}

bool Blaze::Ui::Color(const char* label, ImVec4* color, bool alpha, const char* description)
{
    const float swatch = ImGui::GetFrameHeight();
    Row(label, swatch * 1.6f);
    ImGuiColorEditFlags flags = ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoLabel | ImGuiColorEditFlags_AlphaPreviewHalf;
    flags |= alpha ? ImGuiColorEditFlags_AlphaBar : ImGuiColorEditFlags_NoAlpha;
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(swatch * 0.8f - S(1), ImGui::GetStyle().FramePadding.y));
    const std::string id = std::string("##") + label;
    const bool changed = ImGui::ColorEdit4(id.c_str(), &color->x, flags);
    ImGui::PopStyleVar();
    Description(description);
    return changed;
}

bool Blaze::Ui::Keybind(const char* label, int* key, const char* description)
{
    const float width = ControlWidth();
    Row(label, width);
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

    const std::string text = std::string(ICON_LC_KEYBOARD "  ") + (waiting ? "Press a key..." : Input::KeyName(*key)) + "##" + label;
    if (Button(text.c_str(), waiting ? ButtonKind::Primary : ButtonKind::Secondary, ImVec2(width, 0.0f)) && !waiting)
        g_capturingKey = id;

    Description(description);
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
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 10.0f * scale);
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

Blaze::MenuPage& Blaze::Menu::AddPage(const char* title, const char* icon, std::function<void()> draw, const char* description, const char* group)
{
    MenuPage page;
    page.Title = title;
    page.Icon = icon ? icon : "";
    page.Draw = std::move(draw);
    page.Description = description ? description : "";
    page.Group = group ? group : "";
    pages.push_back(std::move(page));
    return pages.back();
}

void Blaze::Menu::SetSelected(int index)
{
    index = std::clamp(index, 0, std::max(0, static_cast<int>(pages.size()) - 1));
    if (index == selected)
        return;
    selected = index;
    pageProgress = 0.0f;
}

void Blaze::Menu::Select(const char* title)
{
    for (int i = 0; i < static_cast<int>(pages.size()); ++i)
        if (pages[i].Title == title)
            return SetSelected(i);
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
    const ImVec2 windowSize = ImGui::GetWindowSize();
    windowCenter = origin + windowSize * 0.5f;
    const float sidebar = SidebarWidth * scale;
    ImDrawList* draw = ImGui::GetWindowDrawList();
    draw->AddRectFilled(origin, origin + ImVec2(sidebar, windowSize.y), Theme::U32(p.Sidebar), ImGui::GetStyle().WindowRounding,
        ImDrawFlags_RoundCornersLeft);
    draw->AddLine(origin + ImVec2(sidebar, 0.0f), origin + ImVec2(sidebar, windowSize.y), Theme::U32(p.Border));

    DrawSidebar(sidebar, windowSize.y);
    ImGui::SetCursorPos(ImVec2(sidebar + 1.0f, 0.0f));
    DrawContent(windowSize - ImVec2(sidebar + 1.0f, 0.0f));
    ImGui::End();
}

void Blaze::Menu::DrawSidebar(float width, float height)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImGui::SetCursorPos(ImVec2(0.0f, 0.0f));
    ImGui::BeginChild("##sidebar", ImVec2(width, height), ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground | ImGuiWindowFlags_NoScrollbar);
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float pad = 14.0f * scale;
    const ImVec2 origin = ImGui::GetWindowPos();

    // brand: an accent tile with the logo, the title and an optional subtitle
    const float mark = 32.0f * scale;
    const ImVec2 markMin = origin + ImVec2(pad + 2.0f * scale, 20.0f * scale);
    draw->AddRectFilled(markMin, markMin + ImVec2(mark, mark), Theme::U32(p.Accent), 9.0f * scale);
    const ImVec2 logoSize = Fonts::IconLarge.CalcTextSize(Logo.c_str());
    draw->AddText(Fonts::IconLarge.font, Fonts::IconLarge.Px(), markMin + (ImVec2(mark, mark) - logoSize) * 0.5f, Theme::U32(p.OnAccent), Logo.c_str());
    const float textX = markMin.x + mark + 11.0f * scale;
    if (Subtitle.empty())
        draw->AddText(Fonts::SemiBold.font, Fonts::SemiBold.Px(), ImVec2(textX, markMin.y + (mark - Fonts::SemiBold.Px()) * 0.5f), Theme::U32(p.Text),
            Title.c_str());
    else
    {
        draw->AddText(Fonts::SemiBold.font, Fonts::SemiBold.Px(), ImVec2(textX, markMin.y - 1.0f * scale), Theme::U32(p.Text), Title.c_str());
        draw->AddText(Fonts::Small.font, Fonts::Small.Px(), ImVec2(textX, markMin.y + mark - Fonts::Small.Px() + 1.0f * scale), Theme::U32(p.TextMuted),
            Subtitle.c_str());
    }

    // Navigation. Items draw on channel 1 and the gliding selection pill on channel 0 beneath them.
    float y = 20.0f * scale + mark + 22.0f * scale;
    const float itemHeight = 34.0f * scale;
    const float itemWidth = width - pad * 2.0f;
    float selectedY = 0.0f;
    const std::string* group = nullptr;
    draw->ChannelsSplit(2);
    draw->ChannelsSetCurrent(1);

    for (int i = 0; i < static_cast<int>(pages.size()); ++i)
    {
        const MenuPage& page = pages[i];
        if (!group || page.Group != *group)
        {
            group = &page.Group;
            if (!group->empty())
            {
                if (i > 0)
                    y += 10.0f * scale;
                draw->AddText(Fonts::SmallSemiBold.font, Fonts::SmallSemiBold.Px(), origin + ImVec2(pad + 10.0f * scale, y + 4.0f * scale),
                    Theme::U32(p.TextFaint), group->c_str());
                y += 26.0f * scale;
            }
        }

        ImGui::SetCursorPos(ImVec2(pad, y));
        ImGui::PushID(i);
        if (ImGui::InvisibleButton("##nav", ImVec2(itemWidth, itemHeight)))
            SetSelected(i);
        const bool hovered = ImGui::IsItemHovered();
        if (hovered)
            ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
        const float hover = Anim::Toggle(ImGui::GetID("##hover"), hovered && i != selected, 18.0f);
        const float active = Anim::Toggle(ImGui::GetID("##active"), i == selected, 16.0f);
        ImGui::PopID();

        const ImVec2 min = origin + ImVec2(pad, y);
        if (hover > 0.001f)
            draw->AddRectFilled(min, min + ImVec2(itemWidth, itemHeight), Theme::U32(p.Field, hover * 0.55f), 8.0f * scale);
        const float textY = min.y + (itemHeight - ImGui::GetTextLineHeight()) * 0.5f;
        const ImVec4 iconColor = Anim::Lerp(Anim::Lerp(p.TextMuted, p.Text, hover), p.Accent, active);
        const ImVec4 labelColor = Anim::Lerp(Anim::Lerp(p.TextMuted, p.Text, hover), p.Text, active);
        draw->AddText(ImVec2(min.x + 11.0f * scale, textY), Theme::U32(iconColor), page.Icon.c_str());
        const Font& labelFont = active > 0.5f ? Fonts::Medium : Fonts::Regular;
        draw->AddText(labelFont.font, Fonts::Regular.Px(), ImVec2(min.x + 40.0f * scale, textY), Theme::U32(labelColor), page.Title.c_str());

        if (!page.Badge.empty())
        {
            const ImVec2 badgeSize = Fonts::Small.CalcTextSize(page.Badge.c_str()) + ImVec2(12.0f, 4.0f) * scale;
            const ImVec2 badgeMin(min.x + itemWidth - badgeSize.x - 8.0f * scale, min.y + (itemHeight - badgeSize.y) * 0.5f);
            draw->AddRectFilled(badgeMin, badgeMin + badgeSize, Theme::U32(p.Accent, 0.16f), badgeSize.y * 0.5f);
            draw->AddText(Fonts::Small.font, Fonts::Small.Px(), badgeMin + ImVec2(6.0f, 2.0f) * scale, Theme::U32(p.Accent), page.Badge.c_str());
        }

        if (i == selected)
            selectedY = y;
        y += itemHeight + 2.0f * scale;
    }

    draw->ChannelsSetCurrent(0);
    if (!pages.empty())
    {
        const float pillY = Anim::To(ImGui::GetID("##pill"), selectedY, 18.0f);
        const ImVec2 pillMin = origin + ImVec2(pad, pillY);
        draw->AddRectFilled(pillMin, pillMin + ImVec2(itemWidth, itemHeight), Theme::U32(p.Field), 8.0f * scale);
    }
    draw->ChannelsMerge();

    // footer: custom content, then the theme switch
    const float footer = height - pad - ImGui::GetFrameHeight();
    if (SidebarFooter)
    {
        // just above the theme switch, using the height it took on the previous frame
        const float bottom = ShowThemeSwitch ? footer - 10.0f * scale : height - pad;
        ImGui::SetCursorPos(ImVec2(pad, std::max(y + 8.0f * scale, bottom - footerHeight)));
        ImGui::PushItemWidth(itemWidth);
        ImGui::BeginGroup();
        SidebarFooter();
        ImGui::EndGroup();
        ImGui::PopItemWidth();
        footerHeight = ImGui::GetItemRectSize().y;
    }
    if (ShowThemeSwitch)
    {
        ImGui::SetCursorPos(ImVec2(pad, footer));
        int mode = Theme::IsDark() ? 1 : 0;
        static const char* const items[] = { ICON_LC_SUN "  Light", ICON_LC_MOON "  Dark" };
        if (Ui::Segmented("##theme", &mode, items, itemWidth))
            Theme::Set(mode == 1 ? ThemeMode::Dark : ThemeMode::Light);
    }
    ImGui::EndChild();
}

void Blaze::Menu::DrawContent(const ImVec2& size)
{
    const float scale = Ui::Scale;
    const Palette& p = Theme::Colors();
    ImGui::BeginChild("##content", size, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground | ImGuiWindowFlags_NoScrollbar);
    const ImVec2 origin = ImGui::GetWindowPos();
    ImDrawList* draw = ImGui::GetWindowDrawList();
    const float pad = 26.0f * scale;
    const float headerHeight = 72.0f * scale;
    MenuPage* page = Page(selected);

    pageProgress = std::min(1.0f, pageProgress + Anim::Delta() / 0.26f);
    const float t = Anim::EaseOutCubic(pageProgress);

    // header: page title + description, actions on the right
    if (page)
    {
        const float titleY = page->Description.empty() ? (headerHeight - Fonts::Heading.Px()) * 0.5f : 17.0f * scale;
        const float slide = (1.0f - t) * 6.0f * scale;
        draw->AddText(Fonts::Heading.font, Fonts::Heading.Px(), origin + ImVec2(pad + slide, titleY), Theme::U32(p.Text, t), page->Title.c_str());
        if (!page->Description.empty())
            draw->AddText(origin + ImVec2(pad + slide, titleY + Fonts::Heading.Px() + 3.0f * scale), Theme::U32(p.TextMuted, t), page->Description.c_str());
    }

    const float button = ImGui::GetFrameHeight();
    float x = size.x - pad + 6.0f * scale;
    if (ShowCloseButton)
    {
        x -= button;
        ImGui::SetCursorPos(ImVec2(x, (headerHeight - button) * 0.5f));
        if (Ui::IconButton(ICON_LC_X, "##close", "Hide menu") && OnClose)
            OnClose();
    }
    if (HeaderActions)
    {
        // laid out right-to-left into a group measured on the previous frame
        const float actionsWidth = Anim::To(ImGui::GetID("##actionsWidth"), lastActionsWidth, 30.0f);
        ImGui::SetCursorPos(ImVec2(x - actionsWidth - 4.0f * scale, (headerHeight - button) * 0.5f));
        ImGui::BeginGroup();
        HeaderActions();
        ImGui::EndGroup();
        lastActionsWidth = ImGui::GetItemRectSize().x;
    }
    draw->AddLine(origin + ImVec2(0.0f, headerHeight), origin + ImVec2(size.x, headerHeight), Theme::U32(p.Border));

    // Page body: fades and rises in after a page switch. Each page keeps its own scroll position.
    ImGui::SetCursorPos(ImVec2(0.0f, headerHeight + 1.0f));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(pad, 18.0f * scale));
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, ImGui::GetStyle().Alpha * t);
    ImGuiWindowFlags pageFlags = ImGuiWindowFlags_NoBackground;
    if (page && !page->Scroll)
        pageFlags |= ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse;
    char id[32];
    snprintf(id, sizeof(id), "##page%d", selected);
    ImGui::BeginChild(id, ImVec2(size.x, size.y - headerHeight - 1.0f), ImGuiChildFlags_AlwaysUseWindowPadding, pageFlags);
    ImGui::PopStyleVar(2);
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, ImGui::GetStyle().Alpha * t);
    if (t < 1.0f)
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() + (1.0f - t) * 10.0f * scale);
    if (page && page->Draw)
        page->Draw();
    if (page && page->Scroll)
        ImGui::Dummy(ImVec2(0.0f, 6.0f * scale));
    ImGui::PopStyleVar();
    ImGui::EndChild();
    ImGui::EndChild();
}

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// overlay
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

Blaze::Overlay::Overlay(const OverlayOptions& options) : options(options), menuVisible(options.StartVisible)
{
    menu.Title = options.Title;
    menu.Subtitle = options.Subtitle;
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
