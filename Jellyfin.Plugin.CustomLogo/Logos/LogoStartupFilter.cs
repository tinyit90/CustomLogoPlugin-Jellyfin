using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.CustomLogo.Logos;

/// <summary>
/// Puts <see cref="LogoReplacementMiddleware"/> at the front of the server's pipeline,
/// ahead of the static file handler that serves the web client.
/// </summary>
public sealed class LogoStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<LogoReplacementMiddleware>();
            next(app);
        };
    }
}
