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

        var rangeText = from.HasValue || to.HasValue
            ? $"{(from.HasValue ? from.Value.ToString("dd-MMM-yyyy") : "Start")} to {(to.HasValue ? to.Value.ToString("dd-MMM-yyyy") : "Today")}"
            : "All time";

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(settings.CompanyName.ToUpperInvariant()).FontSize(20).Bold();
                    if (!string.IsNullOrWhiteSpace(settings.Address))
                    {
                        col.Item().AlignCenter().PaddingTop(1).Text(settings.Address).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    }
                    col.Item().AlignCenter().PaddingTop(8).Text("PARTY LEDGER STATEMENT").FontSize(12).Bold().Underline();

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(party.FullName).FontSize(12).SemiBold();
                            if (!string.IsNullOrWhiteSpace(party.PrimaryContact))
                            {
                                c.Item().Text($"Contact: {party.PrimaryContact}").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            }
                            if (!string.IsNullOrWhiteSpace(party.BusinessAddress))
                            {
                                c.Item().Text(party.BusinessAddress).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            }
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Period: {rangeText}").FontSize(8);
                            c.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}").FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        void SummaryCard(string label, decimal value, string background, string accent)
                        {
                            row.RelativeItem().Padding(3).Border(1).BorderColor(accent)
                                .Background(background).Padding(6).Column(c =>
                            {
                                c.Item().Text(label).FontSize(7).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                                c.Item().PaddingTop(2).Text(value.ToString("N0")).FontSize(13).Bold().FontColor(accent);
                            });
                        }

                        SummaryCard("Receivable (they owe us)", balance.Receivable, QuestPDF.Helpers.Colors.Green.Lighten5, QuestPDF.Helpers.Colors.Green.Darken2);
                        SummaryCard("Payable (we owe them)", balance.Payable, QuestPDF.Helpers.Colors.Red.Lighten5, QuestPDF.Helpers.Colors.Red.Darken2);
                        SummaryCard("Commission receivable", balance.CommissionReceivable, QuestPDF.Helpers.Colors.Amber.Lighten5, QuestPDF.Helpers.Colors.Amber.Darken3);
                        SummaryCard("Commission received", balance.CommissionReceived, QuestPDF.Helpers.Colors.Blue.Lighten5, QuestPDF.Helpers.Colors.Blue.Darken2);
                    });
                });

                page.Content().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(4.5f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(2f);
                    });

                    table.Header(h =>
                    {
                        void HeaderCell(string text, bool right = false)
                        {
                            // Border/background go on the full-width cell first; alignment only
                            // affects the text inside it, same pattern as the invoice PDF table.
                            var cell = h.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Black)
                                .Background(QuestPDF.Helpers.Colors.Grey.Darken3).Padding(4);
                            (right ? cell.AlignRight() : cell).Text(text).Bold().FontSize(8).FontColor(QuestPDF.Helpers.Colors.White);
                        }

                        HeaderCell("Date");
                        HeaderCell("Document");
                        HeaderCell("Description");
                        HeaderCell("Head");
                        HeaderCell("Debit", right: true);
                        HeaderCell("Credit", right: true);
                        HeaderCell("Receivable", right: true);
                        HeaderCell("Payable", right: true);
                    });

                    var rowIndex = 0;
                    void Cell(string text, bool right = false, bool shaded = false)
                    {
                        var cell = table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4);
                        if (shaded)
                        {
                            cell = cell.Background(QuestPDF.Helpers.Colors.Grey.Lighten4);
                        }
                        (right ? cell.AlignRight() : cell).Text(text).FontSize(8);
                    }

                    foreach (var line in lines)
                    {
                        if (line.Side == DuesSide.Receivable) runningReceivable += line.Debit - line.Credit;
                        if (line.Side == DuesSide.Payable) runningPayable += line.Credit - line.Debit;
                        var shaded = rowIndex++ % 2 == 1;

                        Cell(line.Date.ToString("dd-MMM-yyyy"), shaded: shaded);
                        Cell(line.Document, shaded: shaded);
                        Cell(line.Description, shaded: shaded);
                        Cell(line.Head.GetDisplayName(), shaded: shaded);
                        Cell(line.Debit != 0 ? line.Debit.ToString("N0") : "-", right: true, shaded: shaded);
                        Cell(line.Credit != 0 ? line.Credit.ToString("N0") : "-", right: true, shaded: shaded);
                        Cell(line.Side == DuesSide.Receivable ? runningReceivable.ToString("N0") : "-", right: true, shaded: shaded);
                        Cell(line.Side == DuesSide.Payable ? runningPayable.ToString("N0") : "-", right: true, shaded: shaded);
                    }
                });

                page.Footer().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.Span(" of ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Text($"{lines.Count} entries").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", $"PartyLedger_{party.FullName}_{DateTime.Now:yyyyMMdd}.pdf");
    }

    public async Task<IActionResult> Expenses(int? expenseTypeId, DateTime? from, DateTime? to)
        => View(await LoadExpenseReportAsync(expenseTypeId, from, to));

    public async Task<IActionResult> ExpensesPdf(int? expenseTypeId, DateTime? from, DateTime? to)
    {
        var model = await LoadExpenseReportAsync(expenseTypeId, from, to);
        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
        var typeName = model.ExpenseTypeId.HasValue
            ? model.Types.FirstOrDefault(t => t.Id == model.ExpenseTypeId)?.Name ?? "Unknown"
            : "All types";
        var rangeText = model.From.HasValue || model.To.HasValue
            ? $"{(model.From.HasValue ? model.From.Value.ToString("dd-MMM-yyyy") : "Start")} to {(model.To.HasValue ? model.To.Value.ToString("dd-MMM-yyyy") : "Today")}"
            : "All time";

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(settings.CompanyName.ToUpperInvariant()).FontSize(20).Bold();
                    if (!string.IsNullOrWhiteSpace(settings.Address))
                    {
                        col.Item().AlignCenter().PaddingTop(1).Text(settings.Address).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    }
                    col.Item().AlignCenter().PaddingTop(8).Text("EXPENSE REPORT").FontSize(12).Bold().Underline();
                    col.Item().AlignCenter().PaddingTop(4).Text($"Period: {rangeText}").FontSize(10).SemiBold();
                    col.Item().AlignCenter().PaddingTop(1).Text($"Expense type: {typeName}").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(4.5f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(2.2f);
                    });

                    table.Header(h =>
                    {
                        void HeaderCell(string text, bool right = false)
                        {
                            var cell = h.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Black)
                                .Background(QuestPDF.Helpers.Colors.Grey.Darken3).Padding(4);
                            (right ? cell.AlignRight() : cell).Text(text).Bold().FontSize(8).FontColor(QuestPDF.Helpers.Colors.White);
                        }

                        HeaderCell("Expense #");
                        HeaderCell("Date");
                        HeaderCell("Type");
                        HeaderCell("Description");
                        HeaderCell("Mode");
                        HeaderCell("Amount", right: true);
                    });

                    var rowIndex = 0;
                    void Cell(string text, bool right = false, bool shaded = false, bool bold = false)
                    {
                        var cell = table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4);
                        if (shaded)
                        {
                            cell = cell.Background(QuestPDF.Helpers.Colors.Grey.Lighten4);
                        }
                        var descriptor = (right ? cell.AlignRight() : cell).Text(text).FontSize(8);
                        if (bold) descriptor.Bold();
                    }

                    foreach (var item in model.Rows)
                    {
                        var shaded = rowIndex++ % 2 == 1;
                        Cell(item.ExpenseNumber, shaded: shaded);
                        Cell(item.ExpenseDate.ToString("dd-MMM-yyyy"), shaded: shaded);
                        Cell(item.ExpenseType?.Name ?? "-", shaded: shaded);
                        Cell(item.Description ?? "", shaded: shaded);
                        Cell(item.Mode.GetDisplayName(), shaded: shaded);
                        Cell(item.Amount.ToString("N0"), right: true, shaded: shaded);
                    }

                    if (!model.Rows.Any())
                    {
                        table.Cell().ColumnSpan(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                            .Padding(8).AlignCenter().Text("No expenses match these filters.").FontSize(8);
                    }
                    else
                    {
                        table.Cell().ColumnSpan(5).Border(1).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .Background(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Total").Bold().FontSize(9);
                        table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Black)
                            .Background(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(4).AlignRight().Text(model.Total.ToString("N0")).Bold().FontSize(9);
                    }
                });

                page.Footer().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.Span(" of ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Text($"{model.Rows.Count} expense{(model.Rows.Count == 1 ? "" : "s")} · Generated {DateTime.Now:dd-MMM-yyyy hh:mm tt}")
                        .FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", $"ExpenseReport_{DateTime.Now:yyyyMMdd}.pdf");
    }

    private async Task<ExpenseReportViewModel> LoadExpenseReportAsync(int? expenseTypeId, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            (from, to) = (to, from);
        }

        var query = _db.Expenses.AsNoTracking().AsQueryable();
        if (expenseTypeId.HasValue) query = query.Where(e => e.ExpenseTypeId == expenseTypeId.Value);
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

        var rows = await query
            .Include(e => e.ExpenseType)
            .Include(e => e.BankAccount)
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
            .ToListAsync();

        var model = new ExpenseReportViewModel
        {
            ExpenseTypeId = expenseTypeId,
            From = from,
            To = to,
            Types = await _db.ExpenseTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(),
            Rows = rows,
            Total = rows.Sum(e => e.Amount),
            ByType = rows
                .GroupBy(e => e.ExpenseType?.Name ?? "Unknown")
                .Select(g => new ExpenseTypeTotal { Name = g.Key, Amount = g.Sum(e => e.Amount), Count = g.Count() })
                .OrderByDescending(t => t.Amount)
                .ToList()
        };

        return model;
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
