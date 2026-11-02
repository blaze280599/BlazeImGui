# BlazeImGui

Themed, animated Dear ImGui menu for ImGui.NET: a four-column menu (icon rail, tabs, controls, status column),
light and dark themes with a cross-fade, animated widgets, toasts, and the Inter typeface with 1,544 Lucide icons
embedded in the DLL.

Requires `net8.0-windows`, `x64` and ImGui.NET 1.91.6.1. This package works in any host that owns an ImGui
context (an injected renderer hook, a game engine, your own window). For a ready-made transparent overlay window
use the **BlazeImGui.Overlay** package, which depends on this one.

```csharp
using BlazeImGui;

// once, before the host builds its font atlas:
Ui.Scale = 1f;
Fonts.Load();
Theme.Set(ThemeMode.Light, animate: false);
Theme.ApplyStyle();
var menu = new Menu();
menu.AddPage("Visuals", Icons.Eye, "Overlay").AddTab("General", () => Ui.Toggle("Enabled", ref enabled));

// every frame, inside the host's ImGui frame:
Input.Update();
Theme.Update();
menu.Draw(visibility: 1f, interactive: true);
Notify.Draw();
```

Full guide: https://github.com/blaze280599/BlazeImGui/blob/main/INTEGRATION.md

Fonts: Inter (SIL Open Font License 1.1) and Lucide (ISC); licenses are in the package's `licenses` folder.
