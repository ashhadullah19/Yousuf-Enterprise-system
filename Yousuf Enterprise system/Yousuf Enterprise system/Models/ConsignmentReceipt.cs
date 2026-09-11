using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class ConsignmentReceipt
{
    public int Id { get; set; }

    [Display(Name = "Receipt Date"), DataType(DataType.Date)]
    public DateTime ReceiptDate { get; set; } = DateTime.Today;

    [Display(Name = "Stock Owner"), Range(1, int.MaxValue, ErrorMessage = "Stock owner is required.")]
    public int StockOwnerId { get; set; }
    public Party? StockOwner { get; set; }

    [Display(Name = "Product"), Range(1, int.MaxValue, ErrorMessage = "Product is required.")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Received Quantity"), Range(0.0001, double.MaxValue, ErrorMessage = "Received quantity must be greater than 0."), Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; }

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.KG;

    [Display(Name = "Agreed Commission %"), Range(0, 100, ErrorMessage = "Commission % must be between 0 and 100."), Column(TypeName = "decimal(5,2)")]
    public decimal AgreedCommissionPercentage { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
}
