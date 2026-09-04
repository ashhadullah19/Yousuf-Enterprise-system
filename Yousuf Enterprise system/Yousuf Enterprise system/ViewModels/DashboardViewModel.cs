using System.ComponentModel.DataAnnotations;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.ViewModels;

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required]
    public string Role { get; set; } = AppRoles.Accountant;

    public bool IsActive { get; set; } = true;
}

public class DashboardViewModel
{
    public decimal OwnedStockValue { get; set; }
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal TodayCashFlow { get; set; }
    public List<ProductStockAlert> LowStock { get; set; } = new();
    public List<SalesInvoice> RecentInvoices { get; set; } = new();
}

public class UserListItem
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
}

public class ProductStockAlert
{
    public string ProductName { get; set; } = string.Empty;
    public decimal OnHand { get; set; }
    public decimal AlertQty { get; set; }
}
