namespace Jellyfin.Plugin.CustomLogo.Logos;

/// <summary>
/// The places in the web client where Jellyfin shows its branding.
/// </summary>
public enum LogoSlot
{
    /// <summary>
    /// Wide logo (stock: banner-light.png / banner-dark.png). Shown in the page header
    /// and on the splash screen on large displays.
    /// </summary>
    Banner,

    /// <summary>
    /// Square logo (stock: icon-transparent.png). Shown in the side drawer, server
    /// button, TV layout header, small-screen splash and the logo screensaver.
    /// </summary>
    Icon,

    /// <summary>
    /// Browser tab icon and home-screen icons (stock: favicon.ico, touchicon*.png).
    /// </summary>
    Favicon
}
