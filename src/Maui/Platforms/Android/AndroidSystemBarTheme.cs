using Android.App;
using AndroidX.Core.View;
using JiggerJot.Shared.Ui;

namespace JiggerJot.Maui;

/// <summary>
/// The colours behind Android's status and navigation bars, so the status bar reads as part of whatever
/// sits under it: the header (app.css <c>--surface</c>) on the app's screens, the page itself
/// (<c>--app-bg</c>) on the sign-in screens and the boot state, which have no header. Change them with
/// those tokens — REBRANDING.md lists both places. The platform template shipped a sage green here.
/// </summary>
public static class SystemBarColors
{
    public const string Light = "#FFFFFF";       // app.css :root --surface
    public const string Dark = "#1B1F25";        // app.css [data-bs-theme=dark] --surface
    public const string LightGround = "#F6F3EF"; // app.css :root --app-bg
    public const string DarkGround = "#15181C";  // app.css [data-bs-theme=dark] --app-bg

    /// <summary>Paints the strip behind the status bar and picks icons that read on it.</summary>
    public static void Paint(Activity? activity, bool dark, bool ground)
    {
        if (activity?.Window is not { } window) return;

        var colour = (dark, ground) switch
        {
            (true, true) => DarkGround,
            (true, false) => Dark,
            (false, true) => LightGround,
            _ => Light,
        };
        activity.FindViewById(Android.Resource.Id.Content)?.SetBackgroundColor(Android.Graphics.Color.ParseColor(colour));

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null) return;
        controller.AppearanceLightStatusBars = !dark;
        controller.AppearanceLightNavigationBars = !dark;
    }
}

/// <summary>Follows the page's theme (<see cref="Shared.Ui.Components.SystemBarThemeSync"/>).</summary>
public sealed class AndroidSystemBarTheme : ISystemBarTheme
{
    public Task ApplyAsync(string resolvedTheme, bool ground) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            SystemBarColors.Paint(Platform.CurrentActivity, resolvedTheme == "dark", ground));
}
