using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.ViewModels;

public class ExpenseReportViewModel
{
    public int? ExpenseTypeId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<ExpenseType> Types { get; set; } = new();
    public List<ExpenseTypeTotal> ByType { get; set; } = new();
    public List<Expense> Rows { get; set; } = new();
    public decimal Total { get; set; }
}

public class ExpenseTypeTotal
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}
