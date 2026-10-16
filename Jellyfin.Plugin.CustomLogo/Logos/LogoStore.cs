using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CustomLogo.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CustomLogo.Logos;

/// <summary>
/// Stores uploaded logos on disk and keeps them cached in memory.
/// Files live in &lt;config&gt;/plugins/configurations/CustomLogo so they survive plugin updates.
/// </summary>
public sealed class LogoStore : IDisposable
{
    /// <summary>
    /// Maximum accepted upload size in bytes (10 MB).
    /// </summary>
    public const int MaxFileSize = 10 * 1024 * 1024;

    private static readonly (string Extension, string ContentType)[] _formats =
    {
        (".png", "image/png"),
        (".jpg", "image/jpeg"),
        (".gif", "image/gif"),
        (".webp", "image/webp"),
        (".ico", "image/x-icon")
    };

    private readonly ILogger<LogoStore> _logger;
    private readonly ConcurrentDictionary<LogoSlot, StoredLogo?> _cache = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="logger">Logger.</param>
    public LogoStore(IApplicationPaths applicationPaths, ILogger<LogoStore> logger)
    {
        _logger = logger;
        StoragePath = Path.Combine(applicationPaths.PluginConfigurationsPath, "CustomLogo");
    }

    /// <summary>
    /// Gets the folder the logos are stored in.
    /// </summary>
    public string StoragePath { get; }

    /// <summary>
    /// Detects the image format from the file's magic bytes.
    /// SVG is deliberately not accepted: it can carry script and would be served from the server's origin.
    /// </summary>
    /// <param name="data">File contents.</param>
    /// <returns>Extension and content type, or null if the format is not supported.</returns>
    public static (string Extension, string ContentType)? DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return _formats[0];
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return _formats[1];
        }

        if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a')
        {
            return _formats[2];
        }

        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
        {
            return _formats[3];
        }

        if (data.Length >= 6 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0 && (data[4] != 0 || data[5] != 0))
        {
            return _formats[4];
        }

        return null;
    }

    /// <summary>
    /// Gets the logo uploaded for exactly this slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The logo, or null when none is uploaded.</returns>
    public StoredLogo? Get(LogoSlot slot) => _cache.GetOrAdd(slot, LoadFromDisk);

    /// <summary>
    /// Gets the logo that should be served for a slot, applying the fallbacks from the configuration.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>The logo to serve, or null to let Jellyfin serve its own.</returns>
    public StoredLogo? Resolve(LogoSlot slot, PluginConfiguration config)
    {
        if (!config.Enabled)
        {
            return null;
        }

        var logo = Get(slot);
        if (logo is not null)
        {
            return logo;
        }

        return slot switch
        {
            LogoSlot.Banner when config.UseIconAsBannerFallback => Get(LogoSlot.Icon),
            LogoSlot.Favicon when config.UseIconAsFaviconFallback => Get(LogoSlot.Icon),
            _ => null
        };
    }

    /// <summary>
    /// Saves a new logo for a slot, replacing any previous one.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <param name="data">Image file contents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored logo.</returns>
    /// <exception cref="InvalidDataException">The data is empty, too large or not a supported image.</exception>
    public async Task<StoredLogo> SaveAsync(LogoSlot slot, byte[] data, CancellationToken cancellationToken)
    {
        if (data.Length == 0)
        {
            throw new InvalidDataException("The file is empty.");
        }

        if (data.Length > MaxFileSize)
        {
            throw new InvalidDataException("The file is larger than 10 MB.");
        }

        var format = DetectFormat(data)
            ?? throw new InvalidDataException("Unsupported image format. Use PNG, JPEG, GIF, WebP or ICO.");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(StoragePath);
            var target = GetPath(slot, format.Extension);
            var temp = target + ".tmp";
            await File.WriteAllBytesAsync(temp, data, cancellationToken).ConfigureAwait(false);
            DeleteFiles(slot);
            File.Move(temp, target, true);

            var logo = new StoredLogo(data, format.ContentType, File.GetLastWriteTimeUtc(target));
            _cache[slot] = logo;
            _logger.LogInformation("Custom logo for {Slot} updated ({Size} bytes, {ContentType})", slot, data.Length, format.ContentType);
            return logo;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Removes the logo for a slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>True if a logo was removed.</returns>
    public async Task<bool> DeleteAsync(LogoSlot slot)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var removed = DeleteFiles(slot);
            _cache[slot] = null;
            if (removed)
            {
                _logger.LogInformation("Custom logo for {Slot} removed", slot);
            }

            return removed;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();

    private string GetPath(LogoSlot slot, string extension)
        => Path.Combine(StoragePath, slot.ToString().ToLowerInvariant() + extension);

    private bool DeleteFiles(LogoSlot slot)
    {
        var removed = false;
        foreach (var (extension, _) in _formats)
        {
            var path = GetPath(slot, extension);
            if (File.Exists(path))
            {
                File.Delete(path);
                removed = true;
            }
        }

        return removed;
    }

    private StoredLogo? LoadFromDisk(LogoSlot slot)
    {
        foreach (var (extension, contentType) in _formats)
        {
            var path = GetPath(slot, extension);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return new StoredLogo(File.ReadAllBytes(path), contentType, File.GetLastWriteTimeUtc(path));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Could not read custom logo {Path}", path);
                return null;
            }
        }

        return null;
    }
}

/// <summary>
/// A logo held in memory.
/// </summary>
/// <param name="Data">Image bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="LastModified">When the file was written (UTC).</param>
public sealed record StoredLogo(byte[] Data, string ContentType, DateTime LastModified)
{
    /// <summary>
    /// Gets an entity tag that changes whenever the logo changes.
    /// </summary>
    public string ETag { get; } = "\"" + LastModified.Ticks.ToString("x", CultureInfo.InvariantCulture) + "-" + Data.Length.ToString("x", CultureInfo.InvariantCulture) + "\"";
}
