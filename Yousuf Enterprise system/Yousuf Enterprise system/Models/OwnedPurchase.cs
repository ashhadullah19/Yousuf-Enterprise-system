using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class OwnedPurchase
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "GRN Number")]
    public string GrnNumber { get; set; } = string.Empty;

    [Display(Name = "Purchase Date"), DataType(DataType.Date)]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [Display(Name = "Vendor"), Range(1, int.MaxValue, ErrorMessage = "Vendor is required.")]
    public int VendorId { get; set; }
    public Party? Vendor { get; set; }

    [Display(Name = "Product"), Range(1, int.MaxValue, ErrorMessage = "Product is required.")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Quantity (Net Weight)"), Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.KG;

    // Combined weight of the packaging material itself (bags/drums), same unit as Quantity.
    // Purely informational — doesn't affect Quantity, cost, or any calculation.
    [Display(Name = "Total Package Weight"), Range(0, int.MaxValue, ErrorMessage = "Package weight can't be negative.")]
    public int? TotalPackageWeight { get; set; }

    // Net weight (Quantity) + package weight — stored so reports don't need to recompute it.
    [Display(Name = "Gross Weight")]
    public decimal GrossWeight { get; set; }

    // How it was packed (bags/drums) and how many — purely informational, has no effect on
    // Quantity, cost, or any other calculation. Both optional (e.g. loose bulk goods have neither).
    [Display(Name = "Packing Type")]
    public PackingType? PackingType { get; set; }

    [Display(Name = "Packing Count"), Range(0, int.MaxValue, ErrorMessage = "Packing count can't be negative.")]
    public int? PackingCount { get; set; }

    [Display(Name = "Rate Per Unit"), Range(0.0001, double.MaxValue, ErrorMessage = "Rate must be greater than 0."), Column(TypeName = "decimal(18,4)")]
    public decimal RatePerUnit { get; set; }

    [Display(Name = "Total Product Cost"), Column(TypeName = "decimal(18,2)")]
    public decimal TotalProductCost { get; set; }

    [Display(Name = "Freight / Labour"), Column(TypeName = "decimal(18,2)")]
    public decimal FreightLabourCharges { get; set; }

    [Display(Name = "Grand Total"), Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotalAmount { get; set; }

    // --- Vendor payment ---
    [Display(Name = "Payment Type")]
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    [Display(Name = "Due Date"), DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    // Lets a supplier payment reminder be hidden from the dashboard without settling the purchase.
    public bool ReminderDismissed { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [NotMapped]
    public bool IsOverdue => PaymentType == PaymentType.Credit
        && DueDate.HasValue
        && DueDate.Value.Date < DateTime.Today;
}
