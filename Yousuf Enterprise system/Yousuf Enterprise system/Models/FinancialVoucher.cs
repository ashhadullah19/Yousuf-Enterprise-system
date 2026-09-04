using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class FinancialVoucher
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "Voucher Number")]
    public string VoucherNumber { get; set; } = string.Empty;

    [Display(Name = "Voucher Date"), DataType(DataType.Date)]
    public DateTime VoucherDate { get; set; } = DateTime.Today;

    [Display(Name = "Party")]
    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public VoucherType Type { get; set; }

    public HeadType Head { get; set; }

    public PaymentMode Mode { get; set; }

    [StringLength(100), Display(Name = "Reference Number")]
    public string? ReferenceNumber { get; set; }

    [StringLength(100), Display(Name = "Bank Name")]
    public string? BankName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [Display(Name = "Approved by Admin")]
    public bool ApprovedByAdmin { get; set; }
}
