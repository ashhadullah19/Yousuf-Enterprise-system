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

    [Display(Name = "Buyer"), Range(1, int.MaxValue, ErrorMessage = "Buyer is required.")]
    public int BuyerId { get; set; }
    public Party? Buyer { get; set; }

    [Display(Name = "Stock Type")]
    public StockType StockType { get; set; } = StockType.OwnedStock;

    [Display(Name = "Consignment Receipt")]
    public int? ConsignmentReceiptId { get; set; }
    public ConsignmentReceipt? ConsignmentReceipt { get; set; }

    [Display(Name = "Product"), Range(1, int.MaxValue, ErrorMessage = "Product is required.")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Quantity Sold"), Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0."), Column(TypeName = "decimal(18,4)")]
    public decimal QuantitySold { get; set; }

    [Display(Name = "Rate Per Unit"), Range(0.0001, double.MaxValue, ErrorMessage = "Rate must be greater than 0."), Column(TypeName = "decimal(18,4)")]
    public decimal RatePerUnit { get; set; }

    [Display(Name = "Subtotal"), Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalAmount { get; set; }

    // --- Agent / Sales Commission ---
    // Who earns this commission (optional). When set, the accrued commission is posted
    // to that party's own ledger (Head = Commission) so it can be tracked, per party,
    // separately as receivable (accrued but unpaid) vs received (already paid out).
    [Display(Name = "Commission Agent")]
    public int? AgentId { get; set; }
    public Party? Agent { get; set; }

    [Display(Name = "Commission %"), Column(TypeName = "decimal(5,2)")]
    public decimal AgentCommissionPercentage { get; set; }

    [Display(Name = "Commission Amount"), Column(TypeName = "decimal(18,2)")]
    public decimal AgentCommissionAmount { get; set; }

    // --- Extra charges (conveyance, labour, etc.) ---
    [Display(Name = "Extra Charges Description"), StringLength(200)]
    public string? ExtraChargesDescription { get; set; }

    [Display(Name = "Extra Charges Amount"), Column(TypeName = "decimal(18,2)")]
    public decimal ExtraChargesAmount { get; set; }

    // --- Cost & Profit (auto, from Product's cost basis) ---
    [Display(Name = "Cost of Goods Sold"), Column(TypeName = "decimal(18,2)")]
    public decimal CostOfGoodsSold { get; set; }

    [Display(Name = "Profit"), Column(TypeName = "decimal(18,2)")]
    public decimal ProfitAmount { get; set; }

    // --- GST ---
    [Display(Name = "Apply GST")]
    public bool ApplyGst { get; set; }

    [Display(Name = "GST %"), Column(TypeName = "decimal(5,2)")]
    public decimal GstPercentage { get; set; }

    [Display(Name = "GST Amount"), Column(TypeName = "decimal(18,2)")]
    public decimal GstAmount { get; set; }

    [Display(Name = "Grand Total"), Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotalAmount { get; set; }

    // --- Payment ---
    [Display(Name = "Payment Type")]
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    [Display(Name = "Bank Account")]
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    [Display(Name = "Due Date"), DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    // Convenience flag used by dashboard/index � computed in the controller/service,
    // NOT persisted, so it can't drift out of sync with actual payments.
    [NotMapped]
    public bool IsOverdue => PaymentType == PaymentType.Credit
        && DueDate.HasValue
        && DueDate.Value.Date < DateTime.Today;
}