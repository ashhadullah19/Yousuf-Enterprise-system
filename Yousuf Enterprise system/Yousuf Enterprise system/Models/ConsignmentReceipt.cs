using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class ConsignmentReceipt
{
    public int Id { get; set; }

    [Display(Name = "Receipt Date"), DataType(DataType.Date)]
    public DateTime ReceiptDate { get; set; } = DateTime.Today;

    [Display(Name = "Stock Owner")]
    public int StockOwnerId { get; set; }
    public Party? StockOwner { get; set; }

    [Display(Name = "Product")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Received Quantity"), Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; }

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.KG;

    [Display(Name = "Agreed Commission %"), Column(TypeName = "decimal(5,2)")]
    public decimal AgreedCommissionPercentage { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
}
