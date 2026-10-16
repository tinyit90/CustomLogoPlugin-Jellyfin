using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CustomLogo.Logos;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CustomLogo.Api;

/// <summary>
/// Upload, preview and remove custom logos.
/// </summary>
[ApiController]
[Route("CustomLogo")]
public class CustomLogoController : ControllerBase
{
    private readonly LogoStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomLogoController"/> class.
    /// </summary>
    /// <param name="store">Logo store.</param>
    public CustomLogoController(LogoStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Lists which logos are uploaded.
    /// </summary>
    /// <returns>Status per slot.</returns>
    [HttpGet("Logos")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyDictionary<string, LogoInfo>> GetStatus()
    {
        var result = new Dictionary<string, LogoInfo>();
        foreach (var slot in Enum.GetValues<LogoSlot>())
        {
            var logo = _store.Get(slot);
            result[slot.ToString()] = logo is null
                ? new LogoInfo(false, null, 0, null)
                : new LogoInfo(true, logo.ContentType, logo.Data.Length, logo.LastModified);
        }

        return result;
    }

    /// <summary>
    /// Gets the uploaded logo for a slot (no fallbacks applied).
    /// </summary>
    /// <param name="slot">Banner, Icon or Favicon.</param>
    /// <returns>The image.</returns>
    [HttpGet("Logos/{slot}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetLogo([FromRoute] LogoSlot slot)
    {
        var logo = _store.Get(slot);
        if (logo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-cache";
        return File(logo.Data, logo.ContentType, lastModified: logo.LastModified, entityTag: Microsoft.Net.Http.Headers.EntityTagHeaderValue.Parse(logo.ETag));
    }

    /// <summary>
    /// Uploads a logo. Send the raw image file as the request body.
    /// </summary>
    /// <param name="slot">Banner, Icon or Favicon.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new status of the slot.</returns>
    [HttpPost("Logos/{slot}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(LogoStore.MaxFileSize + 1024)]
    [RawBody]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LogoInfo>> UploadLogo([FromRoute] LogoSlot slot, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int read;
            while ((read = await Request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > LogoStore.MaxFileSize)
                {
                    return BadRequest("The file is larger than 10 MB.");
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException ex)
        {
            return StatusCode(ex.StatusCode, "The file is larger than 10 MB.");
        }

        try
        {
            var logo = await _store.SaveAsync(slot, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
            return new LogoInfo(true, logo.ContentType, logo.Data.Length, logo.LastModified);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes the logo for a slot; Jellyfin's own logo is shown again.
    /// </summary>
    /// <param name="slot">Banner, Icon or Favicon.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Logos/{slot}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLogo([FromRoute] LogoSlot slot)
    {
        await _store.DeleteAsync(slot).ConfigureAwait(false);
        return NoContent();
    }
}

/// <summary>
/// Status of one logo slot.
/// </summary>
/// <param name="Exists">Whether a logo is uploaded.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="LastModified">Upload time (UTC).</param>
public sealed record LogoInfo(bool Exists, string? ContentType, long Size, DateTime? LastModified);
