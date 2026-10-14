#pragma once

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// BlazeImGui for C++ — the same minimal, modern menu as the C# library (src/BlazeImGui), for native Dear ImGui hosts
// (DirectX / OpenGL / Vulkan hooks, internal overlays, plain apps). Requires Dear ImGui 1.92+ (dynamic fonts), C++17.
//
//   Blaze::Overlay overlay({ .Title = "My Tool", .Subtitle = "v1.0" });
//   overlay.menu.AddPage("General", ICON_LC_HOUSE, [] { Blaze::Ui::BeginCard("main"); ...; Blaze::Ui::EndCard(); });
//   overlay.Setup();                  // once, after ImGui::CreateContext() and the backends' Init
//   ...every frame, between ImGui::NewFrame() and ImGui::Render():
//   overlay.Render();                 // theme, menu (with its fade) and toasts
//
// Colors come from Theme::Colors() tokens only, so theme switches and fades apply everywhere. Render thread only,
// except Notify::Show, which any thread may call.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

#include <deque>
#include <functional>
#include <optional>
#include <string>

#include "imgui.h"
#include "blaze_icons.h"

namespace Blaze
{
    enum class ThemeMode { Dark, Light };
    /// Semantic color for badges, toasts, stat deltas, progress bars.
    enum class Tone { Neutral, Accent, Success, Warning, Danger };
    /// Visual weight of Ui::Button.
    enum class ButtonKind { Secondary, Primary, Ghost, Danger };
    enum class FontWeight { Regular, Medium, SemiBold, Bold };
    enum class ToastCorner { TopRight, BottomRight, TopLeft, BottomLeft };

    // ── palette ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// The semantic colors of one theme. Widgets read only these tokens; read the live values from Theme::Colors().
    struct Palette
    {
        ImVec4 Background;                       // menu window
        ImVec4 Sidebar;                          // sidebar / secondary panels
        ImVec4 Surface;                          // cards on the background
        ImVec4 Elevated;                         // popups, combo lists, tooltips, toasts
        ImVec4 Field, FieldHover, FieldActive;   // inputs, buttons, tracks
        ImVec4 Raised;                           // a control on a field (segmented thumb)
        ImVec4 Border;                           // hairlines
        ImVec4 Text, TextMuted, TextFaint;
        ImVec4 Accent, AccentHover, OnAccent;
        ImVec4 Knob;                             // toggle / slider knobs
        ImVec4 Success, Warning, Danger;

        static Palette DefaultDark();
        static Palette DefaultLight();
        /// This palette with another accent (and its derived hover shade).
        Palette WithAccent(const ImVec4& accent, bool lightTheme) const;
        static Palette Lerp(const Palette& a, const Palette& b, float t);

        /// "#RRGGBB" or "#RRGGBBAA".
        static ImVec4 Hex(const char* hex, float alpha = 1.0f);
        /// Lightens (positive) or darkens (negative) by mixing toward white or black.
        static ImVec4 Shade(const ImVec4& color, float amount);
        static ImVec4 Alpha(const ImVec4& color, float alpha);
    };

    // ── animation ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// Frame-rate independent easing. Values are keyed by ImGui IDs.
    namespace Anim
    {
        /// Seconds since the last frame, clamped to 0.1 so a stall never makes animations jump.
        float Delta();
        /// Eases the value stored for `id` toward `target`. speed ~ 1 / time constant (10 relaxed, 16 snappy).
        /// The first call starts at `initial` (or at the target when omitted).
        float To(ImGuiID id, float target, float speed = 14.0f, std::optional<float> initial = std::nullopt);
        float To(const char* key, float target, float speed = 14.0f, std::optional<float> initial = std::nullopt);
        /// Toward 1 while `on`, toward 0 otherwise.
        float Toggle(ImGuiID id, bool on, float speed = 14.0f);
        void Set(ImGuiID id, float value);

        inline float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        inline ImVec2 Lerp(const ImVec2& a, const ImVec2& b, float t) { return ImVec2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        inline ImVec4 Lerp(const ImVec4& a, const ImVec4& b, float t)
        {
            return ImVec4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
        }
        float EaseOutCubic(float t);
        float EaseInOutCubic(float t);
    }

    // ── theme ────────────────────────────────────────────────────────────────────────────────────────────────────────

    namespace Theme
    {
        ThemeMode Mode();
        bool IsDark();
        /// Current colors (mid-transition while a switch animates). Read every frame; don't cache across frames.
        const Palette& Colors();
        /// Frame rounding after UI scale.
        float Rounding();

        /// Switches theme; with `animate` every color cross-fades over ~0.3 s.
        void Set(ThemeMode mode, bool animate = true);
        void Toggle();
        /// One accent for both themes (the light theme gets a slightly deeper hover).
        void SetAccent(const ImVec4& accent, bool animate = true);
        /// Edits a whole palette: Theme::Customize(ThemeMode::Dark, [](Palette& p) { p.Background = Palette::Hex("#0E0E10"); });
        void Customize(ThemeMode mode, const std::function<void(Palette&)>& edit);
        /// Called after the mode changes (e.g. to save the choice).
        void OnChanged(std::function<void(ThemeMode)> callback);

        /// Packs a color for draw lists, multiplying in the style alpha (so menu fades and BeginDisabled apply).
        ImU32 U32(const ImVec4& color, float alpha = 1.0f);

        /// Writes sizes, rounding and colors into ImGui's style at a UI scale. Overlay::Setup / SetScale call it.
        void ApplyStyle(float scale = 1.0f);
        /// Advances a running transition. Overlay::Render calls it every frame.
        void Update();
    }

    // ── fonts ────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// An ImGui 1.92 font plus the size (in unscaled pixels) it is used at.
    struct Font
    {
        ImFont* font = nullptr;
        float size = 0.0f;

        /// Its size in pixels after the style's font scale, for ImDrawList::AddText / CalcTextSizeA.
        float Px() const;
        ImVec2 CalcTextSize(const char* text, const char* textEnd = nullptr, float wrapWidth = 0.0f) const;
    };

    /// Embedded Inter (Regular, Medium, SemiBold; Bold via Add) with the Lucide icons merged into every weight, so icons
    /// mix into any string: ImGui::TextUnformatted(ICON_LC_INFO "  Hello").
    namespace Fonts
    {
        inline Font Regular;         // body (the ImGui default font)
        inline Font Medium;          // body, a little emphasis
        inline Font SemiBold;        // card / section titles
        inline Font Small;           // body - 2 px: captions, hints, badges
        inline Font SmallSemiBold;   // section labels
        inline Font Heading;         // page titles
        inline Font IconLarge;       // brand marks, empty states

        float BodySize();

        /// Adds the fonts to the atlas and makes Regular the default. `merge` runs after each weight is added with
        /// its ImFont: merge extra glyph sources there (MergeMode + DstFont), e.g. symbols Inter lacks.
        void Load(float bodySize = 15.0f, const std::function<void(ImFont*)>& merge = {});
        /// Another weight at a size (unscaled px). Weights are shared ImFonts, so this is cheap at any time after Load.
        Font Add(FontWeight weight, float size);

        void Push(const Font& font);
        void Pop();
    }

    // ── input ────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// Global key state through GetAsyncKeyState (Windows virtual-key codes).
    namespace Input
    {
        /// Once per frame (Overlay::Render calls it).
        void Update();
        bool IsDown(int key);
        bool Pressed(int key);    // only on the frame it went down
        bool Released(int key);
        /// The first key pressed this frame, or 0 (Shift/Ctrl/Alt report their left/right codes).
        int AnyPressed();
        /// "Insert", "F5", "Mouse 4", "None".
        std::string KeyName(int key);
    }

    // ── widgets ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// Themed, animated widgets. Labeled controls use one "settings row" layout: label left, control right, optional
    /// description in small muted text below. Text after "##" is hidden but keeps the ID unique. Controls return true
    /// on the frame the value changes. Plain ImGui widgets are themed too and mix freely.
    namespace Ui
    {
        /// UI scale (sizes; fonts follow through style.FontScaleMain). Set it with Overlay::SetScale.
        inline float Scale = 1.0f;
        inline float S(float value) { return value * Scale; }
        inline ImVec2 S(float x, float y) { return ImVec2(x * Scale, y * Scale); }

        // layout
        void Text(const char* text, const ImVec4& color);
        void Text(const char* text, Tone tone);
        void TextWrapped(const char* text);
        /// A quiet label that groups the rows below it.
        void Section(const char* title);
        /// A rounded card that grows to fit its content. Always pair with EndCard.
        void BeginCard(const char* id, const char* title = nullptr, const char* icon = nullptr, float width = 0.0f);
        void EndCard();
        /// Equal columns: NextCell before each cell, EndGrid only when this returned true.
        bool BeginGrid(const char* id, int columns);
        void NextCell();
        void EndGrid();
        void Divider();
        /// Muted wrapped text.
        void Hint(const char* text);
        /// Same line, with the cursor moved so an item `width` wide ends at the right edge.
        void SameLineRight(float width);
        ImVec2 BadgeSize(const char* text);
        void Badge(const char* text, Tone tone = Tone::Neutral);
        /// KPI tile: muted label, big value, optional colored change.
        void Stat(const char* label, const char* value, const char* change = nullptr, Tone changeTone = Tone::Success, const char* icon = nullptr);
        /// Area chart with a hover readout; min/max default to the data range. valueFormat is printf-style.
        void LineChart(const char* id, const float* values, int count, float height = 0.0f, std::optional<float> min = std::nullopt,
            std::optional<float> max = std::nullopt, const char* valueFormat = "%.1f", Tone tone = Tone::Accent);

        // controls
        /// A full-width row with a label and an animated switch; the whole row is clickable.
        bool Toggle(const char* label, bool* value, const char* description = nullptr);
        /// Compact animated checkbox for dense lists.
        bool Checkbox(const char* label, bool* value);
        /// Label + value on one line, the track below. format is printf-style ("%.1f", "%.0f%%").
        bool Slider(const char* label, float* value, float min, float max, const char* format = "%.1f", const char* description = nullptr);
        bool Slider(const char* label, int* value, int min, int max, const char* format = "%d", const char* description = nullptr);
        /// size.x: 0 fits the text, < 0 fills the row. Icons concatenate: ICON_LC_SAVE "  Save".
        bool Button(const char* label, ButtonKind kind = ButtonKind::Secondary, ImVec2 size = ImVec2(0.0f, 0.0f));
        bool IconButton(const char* icon, const char* id, const char* tooltip = nullptr, float size = 0.0f);
        /// Pill tabs with a sliding thumb. width 0 fills (after Row: the control column).
        bool Segmented(const char* id, int* index, const char* const* items, int count, float width = 0.0f);
        template <int N> bool Segmented(const char* id, int* index, const char* const (&items)[N], float width = 0.0f)
        {
            return Segmented(id, index, items, N, width);
        }
        /// Width of the right-aligned control in a settings row.
        float ControlWidth();
        /// A row label; leaves the cursor and item width where a control `controlWidth` wide goes: Row("Mode"); ImGui::Combo(...).
        void Row(const char* label, float controlWidth = 0.0f);
        bool Combo(const char* label, int* index, const char* const* items, int count, const char* description = nullptr);
        template <int N> bool Combo(const char* label, int* index, const char* const (&items)[N], const char* description = nullptr)
        {
            return Combo(label, index, items, N, description);
        }
        bool TextInput(const char* label, std::string* value, const char* hint = "", const char* description = nullptr);
        /// Drag or double-click to type. min == max == 0: unbounded. format is printf-style.
        bool Number(const char* label, float* value, float speed = 0.1f, float min = 0.0f, float max = 0.0f, const char* format = "%.2f",
            const char* description = nullptr);
        bool Number(const char* label, int* value, float speed = 0.2f, int min = 0, int max = 0, const char* description = nullptr);
        /// A swatch that opens a picker.
        bool Color(const char* label, ImVec4* color, bool alpha = true, const char* description = nullptr);
        /// Click, then press a key or mouse button. Esc cancels, Backspace clears (0).
        bool Keybind(const char* label, int* key, const char* description = nullptr);
        /// True while a Keybind waits for a key (hosts should ignore their hotkeys meanwhile).
        bool IsCapturingKey();

        // feedback
        void Progress(float fraction, const char* label = nullptr, Tone tone = Tone::Accent, float height = 0.0f);
        void Spinner(float radius = 0.0f, float thickness = 0.0f);
        /// A fading tooltip for the previous item.
        void Tooltip(const char* text);
        /// A muted info icon with a tooltip; use after ImGui::SameLine().
        void HelpMarker(const char* text);

        ImVec4 ToneColor(Tone tone);
        /// The visible part of a label (before "##").
        std::string Visible(const char* label);
    }

    // ── toasts ───────────────────────────────────────────────────────────────────────────────────────────────────────

    namespace Notify
    {
        inline ToastCorner Corner = ToastCorner::TopRight;
        /// Thread-safe. icon defaults to one matching the tone.
        void Show(const char* title, const char* message = nullptr, Tone tone = Tone::Accent, float seconds = 3.5f, const char* icon = nullptr);
        /// Draws and ages the toasts (Overlay::Render calls it), menu open or not.
        void Draw();
    }

    // ── menu ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    struct MenuPage
    {
        std::string Title;
        std::string Icon;
        std::function<void()> Draw;
        std::string Description;   // muted line under the page title
        std::string Group;         // sidebar group label; consecutive pages with the same group are listed under it
        std::string Badge;         // pill at the end of the sidebar entry ("New", "3")
        /// false: the page gets the whole body without scrolling and lays itself out (its own child windows).
        bool Scroll = true;
    };

    /// The menu window: a sidebar with the brand, page navigation and the theme switch, and a content area with the
    /// page header and the selected page. Pages fade/slide in when switched; the selection pill glides.
    class Menu
    {
    public:
        std::string Title = "Blaze";
        std::string Subtitle;
        std::string Logo = ICON_LC_FLAME;
        ImVec2 Size = ImVec2(820.0f, 540.0f);   // initial size before scale
        ImVec2 MinSize = ImVec2(620.0f, 400.0f);
        bool Resizable = true;
        float SidebarWidth = 210.0f;
        bool ShowThemeSwitch = true;
        bool ShowCloseButton = true;
        std::function<void()> HeaderActions;   // right of the page header, before the close button
        std::function<void()> SidebarFooter;   // bottom of the sidebar, above the theme switch
        std::function<void()> OnClose;         // the header close button

        /// The returned page stays valid (pages are never moved).
        MenuPage& AddPage(const char* title, const char* icon, std::function<void()> draw, const char* description = nullptr,
            const char* group = nullptr);
        const std::deque<MenuPage>& Pages() const { return pages; }
        MenuPage* Page(int index) { return index >= 0 && index < static_cast<int>(pages.size()) ? &pages[index] : nullptr; }

        int Selected() const { return selected; }
        /// Selects a page and plays the page transition.
        void SetSelected(int index);
        void Select(const char* title);

        /// Puts the window back in the middle of the screen (next Draw).
        void Center() { centerPending = true; }
        /// The window's middle, as of the last Draw (dialogs can open there).
        ImVec2 WindowCenter() const { return windowCenter; }

        /// visibility: the open/close fade (0..1). interactive: false while fading out.
        void Draw(float visibility, bool interactive);

    private:
        void DrawSidebar(float width, float height);
        void DrawContent(const ImVec2& size);

        std::deque<MenuPage> pages;
        int selected = 0;
        float pageProgress = 1.0f;
        std::optional<ImVec2> restPosition;
        bool centerPending = false;
        ImVec2 windowCenter = ImVec2(0.0f, 0.0f);
        float lastActionsWidth = 0.0f;
        float footerHeight = 0.0f;
    };

    // ── overlay ──────────────────────────────────────────────────────────────────────────────────────────────────────

    struct OverlayOptions
    {
        std::string Title = "Blaze Overlay";   // brand name in the sidebar
        std::string Subtitle;                  // small line under it
        std::string Logo = ICON_LC_FLAME;
        bool StartVisible = true;
        ThemeMode Theme = ThemeMode::Dark;
        std::optional<ImVec4> Accent;          // default: indigo #6D8CFF
        float FontSize = 15.0f;                // body px before scale
        float UiScale = 1.0f;
        ImVec2 MenuSize = ImVec2(820.0f, 540.0f);
        /// Runs after each Inter weight is added (see Fonts::Load).
        std::function<void(ImFont*)> MergeFonts;
    };

    /// Ties the pieces together for a host that owns the ImGui context and backends (a hook, an app). The host handles
    /// its own input routing and hotkeys (use Ui::IsCapturingKey / io.WantTextInput to stay out of the way).
    class Overlay
    {
    public:
        explicit Overlay(const OverlayOptions& options = {});
        Overlay(const Overlay&) = delete;   // the menu's close button refers back to this overlay
        Overlay& operator=(const Overlay&) = delete;

        Menu menu;

        /// Once, after ImGui::CreateContext() and the backends' Init: fonts, theme, style.
        void Setup();
        /// Every frame between ImGui::NewFrame() and ImGui::Render(): input, theme, the menu with its fade, toasts.
        void Render();

        bool MenuVisible() const { return menuVisible; }
        void SetMenuVisible(bool visible) { menuVisible = visible; }
        void ToggleMenu() { menuVisible = !menuVisible; }
        /// The menu's fade, 0 hidden .. 1 shown.
        float MenuAlpha() const { return menuAlpha; }

        /// Changes the UI scale at runtime (e.g. when the game resolution changes): sizes and fonts.
        void SetScale(float scale);
        const OverlayOptions& Options() const { return options; }

    private:
        OverlayOptions options;
        bool menuVisible = true;
        float menuAlpha = 0.0f;
    };
}
