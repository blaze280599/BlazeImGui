using BlazeImGui;
using BlazeImGui.Demo;

// Usage: BlazeImGui.Demo.exe [--theme light|dark] [--page "Controls"] [--backdrop] [--toast]
//   --backdrop paints a solid backdrop behind the menu (for screenshots); normally the overlay is transparent.
//   Insert toggles the menu, End exits.
var theme = args.SkipWhile(a => a != "--theme").Skip(1).FirstOrDefault() == "light" ? ThemeMode.Light : ThemeMode.Dark;
var page = args.SkipWhile(a => a != "--page").Skip(1).FirstOrDefault();
var backdrop = args.Contains("--backdrop");
if (args.Contains("--toast"))
{
    Notify.Show("Connected", "Attached to the game process.", Tone.Success, 30);
    Notify.Show("Offsets outdated", "Some features are disabled until you update.", Tone.Warning, 30);
}

using var overlay = new DemoOverlay(theme, page, backdrop);
await overlay.Run();
