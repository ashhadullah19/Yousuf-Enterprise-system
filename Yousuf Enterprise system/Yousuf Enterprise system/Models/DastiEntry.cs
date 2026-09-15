using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

// Standalone cash (dasti) register. Deliberately not linked to ledgers, bank accounts, stock or
// any dashboard totals — nothing else in the application reads these records.
public class DastiEntry
{
    public int Id { get; set; }

    [Display(Name = "Date"), DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0."), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Detail { get; set; }

    [Display(Name = "Type")]
    public DastiType Type { get; set; } = DastiType.CashDeposit;
}
