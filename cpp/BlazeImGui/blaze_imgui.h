#pragma once

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// BlazeImGui for C++ — the same minimal, modern menu as the C# library (src/BlazeImGui), for native Dear ImGui hosts
// (DirectX / OpenGL / Vulkan hooks, internal overlays, plain apps). Requires Dear ImGui 1.92+ (dynamic fonts), C++17.
//
//   Blaze::Overlay overlay({ .Title = "My Tool", .Subtitle = "v1.0" });
//   auto& page = overlay.menu.AddPage("Aim", ICON_LC_CROSSHAIR, "Assist");
//   page.AddTab("General", [] { Blaze::Ui::Toggle("Enabled", &enabled, "Shown under ABOUT while hovered"); ... });
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

// Package version (tools/pack-cpp.ps1 names the package after it). Bump it for every release.
#define BLAZE_IMGUI_VERSION "1.1.2"

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
        ImVec4 Knob;                             // slider grips
        ImVec4 Brand;                            // the brand mark's dot; the rest of the menu stays neutral
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
        inline Font Heading;         // stat values, big numbers
        inline Font Title;           // menu column titles (page, tab, status)
        inline Font Brand;           // the rail's brand mark
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
        /// "Insert", "F5", "Mouse 4", "Num /", "None"; punctuation keys show the layout's character, US as fallback.
        std::string KeyName(int key);
    }

    // ── widgets ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// Themed, animated widgets. Labeled controls stack: the label on its own line, the control under it at ControlWidth.
    /// A description shows under ABOUT in the menu's status column while the control is hovered (under the control when
    /// the menu has no status column). Text after "##" is hidden but keeps the ID unique. Controls return true on the
    /// frame the value changes. Plain ImGui widgets are themed too and mix freely.
    namespace Ui
    {
        /// UI scale (sizes; fonts follow through style.FontScaleMain). Set it with Overlay::SetScale.
        inline float Scale = 1.0f;
        inline float S(float value) { return value * Scale; }
        inline ImVec2 S(float x, float y) { return ImVec2(x * Scale, y * Scale); }
        /// The text under ABOUT: the last hovered control's description, or the tab's.
        inline std::string CurrentAbout;
        /// The name of the control CurrentAbout describes, shown above it; empty for the tab's description.
        inline std::string CurrentAboutTitle;
        /// Descriptions under their controls instead of under ABOUT (the menu sets it each frame).
        inline bool InlineDescriptions = false;
        /// Label column of Readout, as a fraction of the row (at least 110 px).
        inline float ReadoutSplit = 0.42f;

        // layout
        void Text(const char* text, const ImVec4& color);
        void Text(const char* text, Tone tone);
        void TextWrapped(const char* text);
        /// A quiet uppercase label ("AIM ASSIST", "ABOUT") heading the rows below it.
        void Caption(const char* text);
        /// A caption that groups the rows below it, with room above when it follows other content.
        void Section(const char* title);
        /// A status line: a muted label and its value in a column to the right; tone colors the value (Neutral: text).
        void Readout(const char* label, const char* value, Tone tone = Tone::Neutral);
        /// Attaches a description to the previous item or group: shown under ABOUT while hovered, below `title` (the
        /// control's name; text after "##" is dropped).
        void About(const char* description, const char* title = nullptr);
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
        /// A checkbox row: the box and its label, the whole row clickable. The settings-style boolean.
        bool Toggle(const char* label, bool* value, const char* description = nullptr);
        /// A compact checkbox sized to its label, for several on one line.
        bool Checkbox(const char* label, bool* value);
        /// The label above, the value in a box and the track beside it. format is printf-style ("%.1f", "%.0f%%"); the value
        /// snaps to the precision it shows. When the track has under 2 px per shown unit, drags snap to round 1-2-5 steps;
        /// Left/Right still step by one shown unit while the slider is hovered or dragged. Clicking the value box types an
        /// exact value: Enter or a click elsewhere applies it (that frame returns true), Escape cancels.
        bool Slider(const char* label, float* value, float min, float max, const char* format = "%.1f", const char* description = nullptr);
        bool Slider(const char* label, int* value, int min, int max, const char* format = "%d", const char* description = nullptr);
        /// Outlined (Secondary) or ink-filled (Primary). size.x: 0 fits the text, < 0 fills the row. ICON_LC_SAVE "  Save".
        bool Button(const char* label, ButtonKind kind = ButtonKind::Secondary, ImVec2 size = ImVec2(0.0f, 0.0f));
        bool IconButton(const char* icon, const char* id, const char* tooltip = nullptr, float size = 0.0f);
        /// A segmented control with a sliding ink thumb. width 0: ControlWidth.
        bool Segmented(const char* id, int* index, const char* const* items, int count, float width = 0.0f);
        template <int N> bool Segmented(const char* id, int* index, const char* const (&items)[N], float width = 0.0f)
        {
            return Segmented(id, index, items, N, width);
        }
        /// Width of a control under its label (the column width, up to 280 px).
        float ControlWidth();
        /// A label on its own line; leaves the cursor and item width where the control goes: Row("Mode"); ImGui::Combo(...).
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
        /// A swatch with its label beside it; the swatch opens a picker.
        bool Color(const char* label, ImVec4* color, bool alpha = true, const char* description = nullptr);
        /// An outlined button reading "Label: Key". Click, then press a key or mouse button. Esc cancels, Backspace clears (0).
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

    /// One entry of a page's tab list and the content drawn when it is selected.
    struct MenuTab
    {
        std::string Title;
        std::function<void()> Draw;
        std::string About;   // under ABOUT in the status column until a control with a description is hovered
    };

    /// One icon on the rail: a titled page with its own tabs and, optionally, its own status readout.
    class MenuPage
    {
    public:
        std::string Title;
        std::string Icon;
        std::string Caption;            // quiet uppercase line under the page title ("ASSIST")
        std::function<void()> Status;   // the status column for this page; empty falls back to Menu::Status
        bool PinBottom = false;         // icon at the bottom of the rail (settings)
        bool Badge = false;             // a small brand dot on the rail icon
        /// false: the tabs get the whole body without scrolling and lay themselves out (their own child windows).
        bool Scroll = true;

        /// The returned tab stays valid (tabs are never moved).
        MenuTab& AddTab(const char* title, std::function<void()> draw, const char* about = nullptr);
        const std::deque<MenuTab>& Tabs() const { return tabs; }
        int SelectedTab() const { return selectedTab; }
        /// Selects a tab and plays the content transition.
        void SetSelectedTab(int index);
        void SelectTab(const char* title);

    private:
        friend class Menu;
        std::deque<MenuTab> tabs;
        int selectedTab = 0;
        bool tabChanged = false;
    };

    /// The menu window, in four columns: an icon rail with the brand mark, the page's title and tabs with the brand
    /// footer, the selected tab's controls, and a status column with live readouts and an ABOUT hint for the hovered
    /// control. The rail tile and tab highlight glide; content fades in when the page or tab changes.
    class Menu
    {
    public:
        std::string Title = "Blaze";      // brand name at the bottom of the tab column
        std::string Subtitle;             // small muted word after it
        std::string Version;              // after the subtitle ("v1.0.0")
        std::string Logo;                 // the rail's brand mark; empty: the first letter of Title
        ImVec2 Size = ImVec2(1000.0f, 600.0f);   // initial size before scale
        ImVec2 MinSize = ImVec2(860.0f, 500.0f);
        bool Resizable = true;
        float RailWidth = 62.0f;
        float NavWidth = 200.0f;
        float StatusWidth = 280.0f;       // the status column; the controls column takes the rest
        /// Without the status column the controls take the rest and descriptions show under their controls.
        bool ShowStatus = true;
        std::string StatusTitle = "Status";
        std::function<void()> Status;     // the status column for pages without their own
        std::string AboutTitle = "About";
        std::function<void()> OnClose;    // RequestClose (e.g. a "Close menu" button)

        /// A rail page; give it tabs with MenuPage::AddTab. The returned page stays valid (pages are never moved).
        MenuPage& AddPage(const char* title, const char* icon, const char* caption = nullptr);
        /// A page with a single tab of the same title.
        MenuPage& AddPage(const char* title, const char* icon, std::function<void()> draw, const char* caption = nullptr);
        const std::deque<MenuPage>& Pages() const { return pages; }
        MenuPage* Page(int index) { return index >= 0 && index < static_cast<int>(pages.size()) ? &pages[index] : nullptr; }

        int Selected() const { return selected; }
        /// Selects a page and plays the page transition.
        void SetSelected(int index);
        /// The first page with this title; "Page/Tab" also selects the tab.
        void Select(const char* title);
        /// Asks the host to hide the menu (calls OnClose).
        void RequestClose() { if (OnClose) OnClose(); }

        /// Puts the window back in the middle of the screen (next Draw).
        void Center() { centerPending = true; }
        /// The window's middle, as of the last Draw (dialogs can open there).
        ImVec2 WindowCenter() const { return windowCenter; }

        /// visibility: the open/close fade (0..1). interactive: false while fading out.
        void Draw(float visibility, bool interactive);

    private:
        void DrawRail(float width, float height);
        void DrawNav(float x, float width, float height);
        void DrawContent(const ImVec2& position, const ImVec2& size);
        void DrawStatus(const ImVec2& position, const ImVec2& size);
        MenuTab* CurrentTab();
        void ResetAbout();

        std::deque<MenuPage> pages;
        int selected = 0;
        float pageProgress = 1.0f;
        float navProgress = 1.0f;
        float aboutProgress = 1.0f;
        std::string aboutShownTitle, aboutShownText;
        std::optional<ImVec2> restPosition;
        struct Placement { ImVec2 pos, size; };
        // Where the user put the window, where it was last frame, and the viewport work area it was last fitted to.
        std::optional<Placement> wanted, placed, workArea;
        bool centerPending = false;
        ImVec2 windowCenter = ImVec2(0.0f, 0.0f);
    };

    // ── overlay ──────────────────────────────────────────────────────────────────────────────────────────────────────

    struct OverlayOptions
    {
        std::string Title = "Blaze Overlay";   // brand name under the tabs
        std::string Subtitle;                  // small muted word after it ("osu!")
        std::string Version;                   // after the subtitle ("v1.0.0")
        std::string Logo;                      // rail brand mark; empty: the first letter of Title
        bool StartVisible = true;
        ThemeMode Theme = ThemeMode::Light;
        std::optional<ImVec4> Accent;          // default: ink (near-black, near-white in dark)
        float FontSize = 15.0f;                // body px before scale
        float UiScale = 1.0f;
        ImVec2 MenuSize = ImVec2(1000.0f, 600.0f);
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
