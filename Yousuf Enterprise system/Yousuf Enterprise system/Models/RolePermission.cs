using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class RolePermission
{
    public int Id { get; set; }

    [Required]
    public string RoleId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Module { get; set; } = string.Empty;

    [Display(Name = "Can View")]
    public bool CanView { get; set; }

    [Display(Name = "Can Edit")]
    public bool CanEdit { get; set; }
}

// The set of permission-gated business modules. A role's access to each is configured on the
// Roles > Permissions page; Super Admin always has full access regardless of these settings.
// Users, Roles, Settings and Backups are deliberately NOT here — they stay hardcoded to
// Super Admin only (see their controllers), since toggling them on for another role would let
// that role manage accounts/roles/company settings, i.e. escalate its own privileges.
public static class Modules
{
    public const string Dashboard = "Dashboard";
    public const string Parties = "Parties";
    public const string Products = "Products";
    public const string OwnedStock = "OwnedStock";
    public const string Sales = "Sales";
    public const string Ledgers = "Ledgers";
    public const string Reports = "Reports";
    public const string BankAccounts = "BankAccounts";
    public const string BankTransactions = "BankTransactions";
    public const string Expenses = "Expenses";

    public static readonly string[] All =
    {
        Dashboard, Parties, Products, OwnedStock, Sales, Ledgers,
        Reports, BankAccounts, BankTransactions, Expenses
    };
}
