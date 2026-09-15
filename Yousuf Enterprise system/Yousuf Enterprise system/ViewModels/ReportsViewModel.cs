namespace Yousuf_Enterprise_system.ViewModels;

public class RankedAmount
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class MonthlySales
{
    public DateTime Month { get; set; }
    public decimal Sales { get; set; }
    public decimal Profit { get; set; }
    public int Count { get; set; }
}

public class ReportsViewModel
{
    public string Period { get; set; } = "year";
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public decimal Sales { get; set; }
    public int InvoiceCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit => GrossProfit - Expenses;
    public decimal? MarginPercent => Subtotal == 0 ? null : GrossProfit / Subtotal * 100;

    // Sales mix is measured on subtotal (before GST) so GST doesn't inflate either side.
    public decimal OwnedSales { get; set; }
    public decimal ConsignmentSales => Subtotal - OwnedSales;
    public decimal GstSales { get; set; }
    public decimal NonGstSales => Subtotal - GstSales;

    public List<MonthlySales> Monthly { get; set; } = new();
    public List<RankedAmount> TopBuyers { get; set; } = new();
    public List<RankedAmount> TopProducts { get; set; } = new();

    // Outstanding balances are a current position, not tied to the selected period.
    public List<RankedAmount> TopReceivables { get; set; } = new();
    public List<RankedAmount> TopPayables { get; set; } = new();
    public decimal TotalReceivable { get; set; }
    public decimal TotalPayable { get; set; }
}
