using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class LedgerEntry
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "Ledger Number")]
    public string LedgerNumber { get; set; } = string.Empty;

    [Display(Name = "Ledger Date"), DataType(DataType.Date)]
    public DateTime LedgerDate { get; set; } = DateTime.Today;

    [Display(Name = "Party"), Range(1, int.MaxValue, ErrorMessage = "Party is required.")]
    public int PartyId { get; set; }
    public Party? Party { get; set; }

    // Optional link so a payment can settle a specific sales invoice's due amount.
    [Display(Name = "Against Invoice")]
    public int? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public LedgerEntryType Type { get; set; }

    public HeadType Head { get; set; }

    public PaymentMode Mode { get; set; }

    [StringLength(100), Display(Name = "Reference Number")]
    public string? ReferenceNumber { get; set; }

    [StringLength(100), Display(Name = "Bank Name")]
    public string? BankName { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0."), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [Display(Name = "Approved by Admin")]
    public bool ApprovedByAdmin { get; set; }
}
