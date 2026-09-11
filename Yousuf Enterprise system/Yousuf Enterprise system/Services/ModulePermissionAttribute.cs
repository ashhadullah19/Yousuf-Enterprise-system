using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Yousuf_Enterprise_system.Services;

// Gates a controller/action on the caller's role having View (default) or Edit access to the
// named module (see Models/RolePermission.cs). Super Admin always passes (see PermissionService).
// Stack a class-level [ModulePermission(Module)] (view) with an action-level
// [ModulePermission(Module, edit: true)] on mutating actions — both run, so the stricter one wins.
public class ModulePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _module;
    private readonly bool _edit;

    public ModulePermissionAttribute(string module, bool edit = false)
    {
        _module = module;
        _edit = edit;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return; // let [Authorize]/the global AuthorizeFilter handle unauthenticated access
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var allowed = await permissionService.HasAccessAsync(context.HttpContext.User, _module, _edit);
        if (!allowed)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
