using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Jellyfin.Plugin.CustomLogo.Api;

/// <summary>
/// Stops MVC from trying to parse the request body as a form, so the action can read
/// the raw image bytes whatever Content-Type the client sends.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RawBodyAttribute : Attribute, IResourceFilter
{
    /// <inheritdoc />
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    /// <inheritdoc />
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
