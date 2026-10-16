using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CustomLogo.Configuration;

/// <summary>
/// Plugin configuration. The images themselves are stored as files next to the
/// configuration (see <see cref="Logos.LogoStore"/>), not inside this XML.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether uploaded logos replace the Jellyfin ones.
    /// When false, the stock logos are served and uploads are kept for later.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the icon should also be used where the
    /// wide banner appears, when no banner has been uploaded.
    /// </summary>
    public bool UseIconAsBannerFallback { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the icon should also be used as favicon,
    /// when no favicon has been uploaded.
    /// </summary>
    public bool UseIconAsFaviconFallback { get; set; } = true;
}
