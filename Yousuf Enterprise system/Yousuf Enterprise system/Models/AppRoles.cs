namespace Yousuf_Enterprise_system.Models;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Accountant = "Accountant";
    public const string Staff = SuperAdmin + "," + Accountant;
}
