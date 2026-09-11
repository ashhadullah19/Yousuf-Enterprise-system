using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public enum TransactionType
{
    Deposit,
    Withdrawal
}

public class BankTransaction
{
    public int Id { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "Bank account is required.")]
    public int BankAccountId { get; set; }
    [ForeignKey(nameof(BankAccountId))]
    public BankAccount? BankAccount { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    [Required]
    public TransactionType Type { get; set; }

    [Required, Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string? ReferenceNumber { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }
}