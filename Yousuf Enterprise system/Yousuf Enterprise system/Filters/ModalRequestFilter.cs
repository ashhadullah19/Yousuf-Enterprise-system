using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Yousuf_Enterprise_system.Filters;

// Backs the popup forms. A form submitted from a popup can't simply follow a redirect (the page
// behind the popup has to reload instead), so a successful POST's redirect is handed back as JSON
// for site.js to navigate to. Every successful POST also gets a toast message if it didn't set one.
public class ModalRequestFilter : IAsyncResultFilter
{
    public const string HeaderName = "X-Modal-Request";

    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IUrlHelperFactory _urlHelperFactory;

    public ModalRequestFilter(ITempDataDictionaryFactory tempDataFactory, IUrlHelperFactory urlHelperFactory)
    {
        _tempDataFactory = tempDataFactory;
        _urlHelperFactory = urlHelperFactory;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method) && IsRedirect(context.Result))
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (!string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                var tempData = _tempDataFactory.GetTempData(context.HttpContext);
                if (!tempData.ContainsKey("Message"))
                {
                    tempData["Message"] = DefaultMessage(context.RouteData.Values["action"]?.ToString());
                }
            }

            if (request.Headers.ContainsKey(HeaderName) && ResolveUrl(context) is { } url)
            {
                context.Result = new JsonResult(new { redirect = url });
            }
        }

        await next();
    }

    private static bool IsRedirect(IActionResult result) =>
        result is RedirectToActionResult or RedirectToRouteResult or RedirectResult or LocalRedirectResult;

    private string? ResolveUrl(ResultExecutingContext context)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(context);
        return context.Result switch
        {
            RedirectToActionResult r => urlHelper.Action(r.ActionName, r.ControllerName, r.RouteValues),
            RedirectToRouteResult r => urlHelper.RouteUrl(r.RouteName, r.RouteValues),
            RedirectResult r => urlHelper.Content(r.Url),
            LocalRedirectResult r => urlHelper.Content(r.Url),
            _ => null
        };
    }

    private static string DefaultMessage(string? action) => action switch
    {
        "Create" => "Created successfully.",
        "Edit" => "Changes saved.",
        "Delete" => "Deleted successfully.",
        "Approve" => "Entry approved.",
        "DismissReminder" => "Reminder dismissed.",
        "Permissions" => "Permissions saved.",
        _ => "Saved successfully."
    };
}
