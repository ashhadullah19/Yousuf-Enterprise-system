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

// A credit purchase whose payment to the vendor is due tomorrow, today, or overdue.
public class PurchasePaymentReminder
{
    public int PurchaseId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public bool IsOverdue { get; set; }
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

// One row in a dashboard reminder card (customer payment, supplier payment or cheque).
public class ReminderItem
{
    public string Title { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? ReferenceUrl { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public string? DismissUrl { get; set; }
    public string? DismissTitle { get; set; }
    public string? DismissMessage { get; set; }
}

public class ReminderPanelViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-bell";
    public string Tone { get; set; } = "amber";
    public string EmptyText { get; set; } = string.Empty;
    public List<ReminderItem> Items { get; set; } = new();

    // When true, the panel has no fixed due date to compare against — items are shown as
    // "Pending N days" (since DueDate, which holds the date it started accruing) instead of
    // the Overdue/Due today/Due tomorrow wording used for actual due dates.
    public bool IsAgeBased { get; set; }
}

// A party with commission accrued but not yet collected back from them.
public class CommissionReminder
{
    public int PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public decimal Outstanding { get; set; }
    public DateTime OldestAccrualDate { get; set; }
    public int PendingEntryCount { get; set; }
}

public class DashboardViewModel
{
    public decimal OwnedStockValue { get; set; }
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal TodayCashFlow { get; set; }
    public decimal CommissionReceivable { get; set; }
    public decimal CommissionReceived { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalExpenses { get; set; }
    public List<ProductStockAlert> LowStock { get; set; } = new();
    public List<Yousuf_Enterprise_system.Models.SalesInvoice> RecentInvoices { get; set; } = new();

    // NEW
    public List<PaymentReminder> PaymentReminders { get; set; } = new();
    public List<PurchasePaymentReminder> PurchaseReminders { get; set; } = new();
    public List<ChequeReminder> ChequeReminders { get; set; } = new();
    public List<CommissionReminder> CommissionReminders { get; set; } = new();
}

public class ProductStockAlert
{
    public string ProductName { get; set; } = string.Empty;
    public decimal OnHand { get; set; }
    public decimal AlertQty { get; set; }
}