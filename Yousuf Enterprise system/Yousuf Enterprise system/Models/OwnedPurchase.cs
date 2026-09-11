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

    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.KG;

    [Display(Name = "Rate Per Unit"), Range(0.0001, double.MaxValue, ErrorMessage = "Rate must be greater than 0."), Column(TypeName = "decimal(18,4)")]
    public decimal RatePerUnit { get; set; }

    [Display(Name = "Total Product Cost"), Column(TypeName = "decimal(18,2)")]
    public decimal TotalProductCost { get; set; }

    [Display(Name = "Freight / Labour"), Column(TypeName = "decimal(18,2)")]
    public decimal FreightLabourCharges { get; set; }

    [Display(Name = "Grand Total"), Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotalAmount { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }
}
