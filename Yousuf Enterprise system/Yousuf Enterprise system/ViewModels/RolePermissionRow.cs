namespace Yousuf_Enterprise_system.ViewModels;

public class RolePermissionRow
{
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
}
