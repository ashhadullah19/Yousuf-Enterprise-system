using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

// One owned-stock lot (GRN) that a sale draws from. A single sale can be filled from several
// vendors' lots at once, each at its own purchase rate and its own commission rate, so cost and
// commission are recorded per source instead of being derived FIFO after the fact.
public class SalesInvoiceAllocation
{
    public int Id { get; set; }

    public int SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    [Display(Name = "Owned Purchase")]
    public int OwnedPurchaseId { get; set; }
    public OwnedPurchase? OwnedPurchase { get; set; }

    [Display(Name = "Quantity"), Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    // Snapshot of the lot's purchase rate at the time of sale, so later edits to the purchase
    // can't silently rewrite the cost/profit of an invoice that's already been issued.
    [Display(Name = "Purchase Rate"), Column(TypeName = "decimal(18,4)")]
    public decimal PurchaseRate { get; set; }

    [Display(Name = "Commission %"), Column(TypeName = "decimal(5,2)")]
    public decimal CommissionPercentage { get; set; }

    [Display(Name = "Commission Amount"), Column(TypeName = "decimal(18,2)")]
    public decimal CommissionAmount { get; set; }

    [NotMapped]
    public decimal Cost => Math.Round(Quantity * PurchaseRate, 2);
}
