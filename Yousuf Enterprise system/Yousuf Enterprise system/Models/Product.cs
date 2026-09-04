using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Unit of Measure")]
    public UnitOfMeasure Uom { get; set; } = UnitOfMeasure.KG;

    [Display(Name = "Minimum Stock Alert Qty")]
    public decimal MinimumStockAlertQty { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
