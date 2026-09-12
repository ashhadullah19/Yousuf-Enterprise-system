using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class Expense
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "Expense Number")]
    public string ExpenseNumber { get; set; } = string.Empty;

    [Display(Name = "Expense Date"), DataType(DataType.Date)]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required, StringLength(100), Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0."), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMode Mode { get; set; } = PaymentMode.Cash;

    // Only meaningful for Mode = Online Bank Transfer — auto-posts a matching withdrawal.
    [Display(Name = "Bank Account")]
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }
}
