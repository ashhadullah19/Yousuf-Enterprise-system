using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Reports)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledger;
    private readonly IExportService _export;
    private readonly IInvoiceService _invoices;

    public ReportsController(ApplicationDbContext db, ILedgerService ledger, IExportService export, IInvoiceService invoices)
    {
        _db = db;
        _ledger = ledger;
        _export = export;
        _invoices = invoices;
    }

    public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        if (from.HasValue || to.HasValue)
        {
            period = "custom";
        }

        DateTime? start = null;
        DateTime? end = null;
        switch (period)
        {
            case "month":
                start = new DateTime(today.Year, today.Month, 1);
                end = today;
                break;
            case "30d":
                start = today.AddDays(-29);
                end = today;
                break;
            case "all":
                break;
            case "custom":
                start = from?.Date;
                end = to?.Date;
                if (start > end)
                {
                    (start, end) = (end, start);
                }
                break;
            default:
                period = "year";
                start = new DateTime(today.Year, 1, 1);
                end = today;
                break;
        }

        var invoices = _db.SalesInvoices.AsNoTracking();
        var expenses = _db.Expenses.AsNoTracking();
        if (start.HasValue)
        {
            var startDate = start.Value;
            invoices = invoices.Where(i => i.InvoiceDate >= startDate);
            expenses = expenses.Where(e => e.ExpenseDate >= startDate);
        }
        if (end.HasValue)
        {
            var endExclusive = end.Value.AddDays(1);
            invoices = invoices.Where(i => i.InvoiceDate < endExclusive);
            expenses = expenses.Where(e => e.ExpenseDate < endExclusive);
        }

        var model = new ReportsViewModel
        {
            Period = period!,
            PeriodLabel = BuildPeriodLabel(period!, start, end),
            From = start,
            To = end,
            Sales = await invoices.SumAsync(i => (decimal?)i.GrandTotalAmount) ?? 0,
            Subtotal = await invoices.SumAsync(i => (decimal?)i.SubtotalAmount) ?? 0,
            GrossProfit = await invoices.SumAsync(i => (decimal?)i.ProfitAmount) ?? 0,
            InvoiceCount = await invoices.CountAsync(),
            OwnedSales = await invoices.Where(i => i.StockType == StockType.OwnedStock).SumAsync(i => (decimal?)i.SubtotalAmount) ?? 0,
            GstSales = await invoices.Where(i => i.ApplyGst).SumAsync(i => (decimal?)i.SubtotalAmount) ?? 0,
            Expenses = await expenses.SumAsync(e => (decimal?)e.Amount) ?? 0
        };

        // Names are looked up ignoring the soft-delete filter so a deleted party or product's
        // past sales still count toward the rankings instead of silently dropping out.
        var buyerTotals = await invoices
            .GroupBy(i => i.BuyerId)
            .Select(g => new { Id = g.Key, Amount = g.Sum(i => i.GrandTotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync();
        var buyerIds = buyerTotals.Select(b => b.Id).ToList();
        var buyerNames = await _db.Parties.IgnoreQueryFilters().AsNoTracking()
            .Where(p => buyerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName);
        model.TopBuyers = buyerTotals
            .Select(b => new RankedAmount { Name = buyerNames.GetValueOrDefault(b.Id, "Unknown"), Amount = b.Amount, Count = b.Count })
            .ToList();

        var productTotals = await invoices
            .GroupBy(i => i.ProductId)
            .Select(g => new { Id = g.Key, Amount = g.Sum(i => i.SubtotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync();
        var productIds = productTotals.Select(p => p.Id).ToList();
        var productNames = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);
        model.TopProducts = productTotals
            .Select(p => new RankedAmount { Name = productNames.GetValueOrDefault(p.Id, "Unknown"), Amount = p.Amount, Count = p.Count })
            .ToList();

        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var monthlyRaw = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= firstMonth)
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Sales = g.Sum(i => i.GrandTotalAmount),
                Profit = g.Sum(i => i.ProfitAmount),
                Count = g.Count()
            })
            .ToListAsync();
        model.Monthly = Enumerable.Range(0, 12)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month =>
            {
                var row = monthlyRaw.FirstOrDefault(r => r.Year == month.Year && r.Month == month.Month);
                return new MonthlySales { Month = month, Sales = row?.Sales ?? 0, Profit = row?.Profit ?? 0, Count = row?.Count ?? 0 };
            })
            .ToList();

        var balances = await _ledger.GetAllPartyBalancesAsync();
        var partyNames = await _db.Parties.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.FullName);
        model.TotalReceivable = Math.Round(balances.Values.Sum(b => b.Receivable), 2);
        model.TotalPayable = Math.Round(balances.Values.Sum(b => b.Payable), 2);
        model.TopReceivables = balances
            .Where(b => b.Value.Receivable > 0 && partyNames.ContainsKey(b.Key))
            .OrderByDescending(b => b.Value.Receivable)
            .Take(5)
            .Select(b => new RankedAmount { Name = partyNames[b.Key], Amount = b.Value.Receivable })
            .ToList();
        model.TopPayables = balances
            .Where(b => b.Value.Payable > 0 && partyNames.ContainsKey(b.Key))
            .OrderByDescending(b => b.Value.Payable)
            .Take(5)
            .Select(b => new RankedAmount { Name = partyNames[b.Key], Amount = b.Value.Payable })
            .ToList();

        return View(model);
    }

    private static string BuildPeriodLabel(string period, DateTime? start, DateTime? end) => period switch
    {
        "month" => $"This month ({start:dd MMM} – {end:dd MMM yyyy})",
        "30d" => $"Last 30 days ({start:dd MMM} – {end:dd MMM yyyy})",
        "year" => $"This year ({start:dd MMM} – {end:dd MMM yyyy})",
        "all" => "All time",
        _ when start.HasValue && end.HasValue => $"{start:dd MMM yyyy} – {end:dd MMM yyyy}",
        _ when start.HasValue => $"From {start:dd MMM yyyy}",
        _ => $"Up to {end:dd MMM yyyy}"
    };

    public async Task<IActionResult> PartyLedger(int? partyId, DateTime? from, DateTime? to)
    {
        ViewBag.Parties = await _db.Parties.AsNoTracking().OrderBy(p => p.FullName).ToListAsync();
        ViewBag.PartyId = partyId;
        ViewBag.From = from;
        ViewBag.To = to;
        if (!partyId.HasValue)
        {
            return View(Array.Empty<PartyLedgerLine>());
        }

        var lines = await _ledger.GetPartyLedgerAsync(partyId.Value, from, to);
        ViewBag.Balance = PartyCommissionBalance.FromLedger(lines);
        return View(lines);
    }

    public async Task<IActionResult> PartyLedgerPdf(int partyId, DateTime? from, DateTime? to)
    {
        var party = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partyId);
        if (party is null)
        {
            return NotFound();
        }

        var lines = await _ledger.GetPartyLedgerAsync(partyId, from, to);
        var balance = PartyCommissionBalance.FromLedger(lines);
        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        decimal runningReceivable = 0;
        decimal runningPayable = 0;

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Column(col =>
                {
                    col.Item().Text(settings.CompanyName).FontSize(16).Bold();
                    col.Item().Text($"Party Ledger — {party.FullName}").FontSize(12).SemiBold();
                    var rangeText = from.HasValue || to.HasValue
                        ? $"{(from.HasValue ? from.Value.ToString("dd-MMM-yyyy") : "Start")} to {(to.HasValue ? to.Value.ToString("dd-MMM-yyyy") : "Today")}"
                        : "All time";
                    col.Item().Text(rangeText).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Date").Bold();
                        h.Cell().Text("Document").Bold();
                        h.Cell().Text("Description").Bold();
                        h.Cell().Text("Head").Bold();
                        h.Cell().AlignRight().Text("Debit").Bold();
                        h.Cell().AlignRight().Text("Credit").Bold();
                        h.Cell().AlignRight().Text("Receivable").Bold();
                        h.Cell().AlignRight().Text("Payable").Bold();
                    });

                    foreach (var line in lines)
                    {
                        if (line.Side == DuesSide.Receivable) runningReceivable += line.Debit - line.Credit;
                        if (line.Side == DuesSide.Payable) runningPayable += line.Credit - line.Debit;

                        table.Cell().Text(line.Date.ToString("dd-MMM-yyyy"));
                        table.Cell().Text(line.Document);
                        table.Cell().Text(line.Description);
                        table.Cell().Text(line.Head.GetDisplayName());
                        table.Cell().AlignRight().Text(line.Debit != 0 ? line.Debit.ToString("N0") : "");
                        table.Cell().AlignRight().Text(line.Credit != 0 ? line.Credit.ToString("N0") : "");
                        table.Cell().AlignRight().Text(line.Side == DuesSide.Receivable ? runningReceivable.ToString("N0") : "");
                        table.Cell().AlignRight().Text(line.Side == DuesSide.Payable ? runningPayable.ToString("N0") : "");
                    }
                });

                page.Footer().PaddingTop(8).Column(col =>
                {
                    col.Item().AlignRight().Text($"Receivable: {balance.Receivable:N0}    Payable: {balance.Payable:N0}").Bold();
                    col.Item().AlignRight().Text($"Commission receivable: {balance.CommissionReceivable:N0}    Commission received: {balance.CommissionReceived:N0}");
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", $"PartyLedger_{party.FullName}_{DateTime.Now:yyyyMMdd}.pdf");
    }

    public async Task<IActionResult> Stock()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var rows = new List<(Product Product, decimal Owned, decimal Consignment)>();
        foreach (var product in products)
        {
            var owned = await _invoices.OwnedStockOnHandAsync(product.Id);
            var consIn = await _db.ConsignmentReceipts.Where(c => c.ProductId == product.Id).SumAsync(c => (decimal?)c.ReceivedQuantity) ?? 0;
            var consOut = await _db.SalesInvoices.Where(s => s.ProductId == product.Id && s.StockType == StockType.ConsignmentStock).SumAsync(s => (decimal?)s.QuantitySold) ?? 0;
            rows.Add((product, owned, consIn - consOut));
        }

        return View(rows);
    }

    public async Task<IActionResult> Export(string type)
    {
        var (bytes, name) = type switch
        {
            "parties" => (await _export.ExportPartiesAsync(), "parties.xlsx"),
            "products" => (await _export.ExportProductsAsync(), "products.xlsx"),
            "invoices" => (await _export.ExportInvoicesAsync(), "invoices.xlsx"),
            _ => (await _export.ExportStockAsync(), "stock.xlsx")
        };
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
}
