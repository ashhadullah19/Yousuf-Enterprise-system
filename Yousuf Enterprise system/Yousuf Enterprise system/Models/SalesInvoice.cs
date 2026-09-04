using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yousuf_Enterprise_system.Models;

public class SalesInvoice
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "Invoice Number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Display(Name = "Invoice Date"), DataType(DataType.Date)]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Display(Name = "Buyer")]
    public int BuyerId { get; set; }
    public Party? Buyer { get; set; }

    [Display(Name = "Stock Type")]
    public StockType StockType { get; set; } = StockType.OwnedStock;

    [Display(Name = "Consignment Receipt")]
    public int? ConsignmentReceiptId { get; set; }
    public ConsignmentReceipt? ConsignmentReceipt { get; set; }

    [Display(Name = "Product")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Quantity Sold"), Column(TypeName = "decimal(18,4)")]
    public decimal QuantitySold { get; set; }

    [Display(Name = "Rate Per Unit"), Column(TypeName = "decimal(18,4)")]
    public decimal RatePerUnit { get; set; }

    [Display(Name = "Subtotal"), Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalAmount { get; set; }

    [Display(Name = "Apply GST")]
    public bool ApplyGst { get; set; }

    [Display(Name = "GST %"), Column(TypeName = "decimal(5,2)")]
    public decimal GstPercentage { get; set; }

    [Display(Name = "GST Amount"), Column(TypeName = "decimal(18,2)")]
    public decimal GstAmount { get; set; }

    [Display(Name = "Grand Total"), Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotalAmount { get; set; }

    [Display(Name = "Commission Revenue"), Column(TypeName = "decimal(18,2)")]
    public decimal CommissionRevenue { get; set; }

    [Display(Name = "Stock Owner Payable"), Column(TypeName = "decimal(18,2)")]
    public decimal StockOwnerPayable { get; set; }
}
