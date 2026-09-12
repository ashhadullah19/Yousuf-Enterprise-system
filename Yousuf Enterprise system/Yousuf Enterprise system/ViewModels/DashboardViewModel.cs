namespace Yousuf_Enterprise_system.ViewModels;

public class PaymentReminder
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public bool IsOverdue { get; set; }

    // "Overdue" / "Due today" / "Due tomorrow" — the old code always showed "Due tomorrow"
    // for anything not overdue, even invoices actually due today.
    public string Status { get; set; } = string.Empty;
}

public class ChequeReminder
{
    public int LedgerEntryId { get; set; }
    public string LedgerNumber { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public DateTime ChequeDate { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
}

public class DashboardViewModel
{
    public decimal OwnedStockValue { get; set; }
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal TodayCashFlow { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalExpenses { get; set; }
    public List<ProductStockAlert> LowStock { get; set; } = new();
    public List<Yousuf_Enterprise_system.Models.SalesInvoice> RecentInvoices { get; set; } = new();

    // NEW
    public List<PaymentReminder> PaymentReminders { get; set; } = new();
    public List<ChequeReminder> ChequeReminders { get; set; } = new();
}

public class ProductStockAlert
{
    public string ProductName { get; set; } = string.Empty;
    public decimal OnHand { get; set; }
    public decimal AlertQty { get; set; }
}