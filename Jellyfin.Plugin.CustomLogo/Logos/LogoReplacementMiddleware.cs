using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.CustomLogo.Logos;

/// <summary>
/// Answers requests for the web client's built-in logo files with the uploaded logo.
/// </summary>
/// <remarks>
/// jellyfin-web bundles its logos from @jellyfin/ux-web. Webpack emits them as
/// <c>banner-light.&lt;hash&gt;.png</c>, <c>icon-transparent.&lt;hash&gt;.png</c>, <c>favicon.&lt;hash&gt;.ico</c>
/// and <c>favicons/touchicon*.png</c>. Matching on the base name instead of the full
/// hashed name keeps this working across web client updates.
/// </remarks>
public sealed partial class LogoReplacementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LogoStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoReplacementMiddleware"/> class.
    /// </summary>
    /// <param name="next">Next middleware.</param>
    /// <param name="store">Logo store.</param>
    public LogoReplacementMiddleware(RequestDelegate next, LogoStore store)
    {
        _next = next;
        _store = store;
    }

    /// <summary>
    /// Maps a request path to the logo slot it belongs to.
    /// </summary>
    /// <param name="path">Request path, possibly including a base URL.</param>
    /// <returns>The slot, or null if the path is not a stock logo.</returns>
    public static LogoSlot? MatchSlot(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var webIndex = path.IndexOf("/web/", StringComparison.OrdinalIgnoreCase);
        if (webIndex < 0)
        {
            return null;
        }

        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var match = StockLogoRegex().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups["name"].Value.ToLowerInvariant();
        return name switch
        {
            "banner-light" or "banner-dark" => LogoSlot.Banner,
            "icon-transparent" => LogoSlot.Icon,
            _ => LogoSlot.Favicon
        };
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && MatchSlot(request.Path.Value) is LogoSlot slot
            && Plugin.Instance?.Configuration is { } config
            && _store.Resolve(slot, config) is StoredLogo logo)
        {
            var response = context.Response;
            response.Headers.CacheControl = "no-cache";
            response.Headers.ETag = logo.ETag;

            if (request.Headers.IfNoneMatch is StringValues inm && inm.Count > 0 && inm.ToString().Contains(logo.ETag, StringComparison.Ordinal))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = logo.ContentType;
            response.ContentLength = logo.Data.Length;
            if (HttpMethods.IsGet(request.Method))
            {
                await response.Body.WriteAsync(logo.Data, context.RequestAborted).ConfigureAwait(false);
            }

            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^(?<name>banner-light|banner-dark|icon-transparent|favicon|touchicon\d*)(\.[0-9a-f]{6,})?\.(png|ico)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StockLogoRegex();
}
