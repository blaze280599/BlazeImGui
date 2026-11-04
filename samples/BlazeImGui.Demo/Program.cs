using BlazeImGui;
using BlazeImGui.Demo;

// Usage: BlazeImGui.Demo.exe [--theme light|dark] [--page "Controls" | "Controls/Sliders"] [--backdrop] [--toast]
//                            [--follow notepad]
//   --backdrop paints a solid backdrop behind the menu (for screenshots); normally the overlay is transparent.
//   --follow keeps the overlay over that process's window (it may start later; until then it covers the screen).
//   Insert toggles the menu, End exits.
var theme = args.SkipWhile(a => a != "--theme").Skip(1).FirstOrDefault() == "dark" ? ThemeMode.Dark : ThemeMode.Light;
var page = args.SkipWhile(a => a != "--page").Skip(1).FirstOrDefault();
var backdrop = args.Contains("--backdrop");
var follow = args.SkipWhile(a => a != "--follow").Skip(1).FirstOrDefault() is { } process
    ? WindowTarget.FromProcessName(process) : null;
if (args.Contains("--toast"))
{
    Notify.Show("Connected", "Attached to the game process.", Tone.Success, 30);
    Notify.Show("Offsets outdated", "Some features are disabled until you update.", Tone.Warning, 30);
}

using var overlay = new DemoOverlay(theme, page, backdrop, follow);
await overlay.Run();
