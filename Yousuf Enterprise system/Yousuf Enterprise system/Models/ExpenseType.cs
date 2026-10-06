using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class ExpenseType
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Expense Type")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
