# BlazeImGui.Overlay

Themed, animated Dear ImGui menu for [ClickableTransparentOverlay](https://www.nuget.org/packages/ClickableTransparentOverlay)
overlays: a four-column menu (icon rail, tabs, controls, status column), light and dark themes with a cross-fade,
animated widgets, toasts, and the Inter typeface with 1,544 Lucide icons embedded in the DLL.

Requires `net8.0-windows` and `x64`. Depends on the `BlazeImGui` package (the menu and widgets); use that package alone in a
host that already owns an ImGui context.

```csharp
using BlazeImGui;

sealed class MyOverlay : BlazeOverlay
{
    bool _enabled = true;

    public MyOverlay() : base(new OverlayOptions { Title = "mytool", Version = "v1.0.0" })
    {
        Menu.AddPage("Visuals", Icons.Eye, "Overlay")
            .AddTab("General", () => Ui.Toggle("Enabled", ref _enabled, "Shown under ABOUT while hovered."));
    }
}
```

Full guide: https://github.com/blaze280599/BlazeImGui/blob/main/INTEGRATION.md

Fonts: Inter (SIL Open Font License 1.1) and Lucide (ISC); licenses are in the BlazeImGui package's `licenses` folder.
