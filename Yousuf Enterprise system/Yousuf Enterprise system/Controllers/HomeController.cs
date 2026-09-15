using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Dashboard)]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledger;

    public HomeController(ApplicationDbContext db, ILedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        // Reminders start one day before the due date and stay until paid or dismissed.
        var reminderCutoff = today.AddDays(1);

        // Every party's balance in one pass; receivables, payables and both commission figures are
        // all read from it rather than recomputing the ledgers for each card.
        var balances = await _ledger.GetAllPartyBalancesAsync();

        var model = new DashboardViewModel
        {
            Receivables = Math.Round(balances.Values.Sum(b => b.Receivable), 2),
            Payables = Math.Round(balances.Values.Sum(b => b.Payable), 2),
            CommissionReceivable = Math.Round(balances.Values.Sum(b => b.CommissionReceivable), 2),
            CommissionReceived = Math.Round(balances.Values.Sum(b => b.CommissionReceived), 2),
            OwnedStockValue = await _ledger.OwnedStockValueAsync(),
            TodayCashFlow = await _ledger.TodayCashFlowAsync(),
            TotalProfit = await _ledger.GetTotalProfitAsync(),
            TotalExpenses = await _db.Expenses.AsNoTracking().SumAsync(e => (decimal?)e.Amount) ?? 0m,
            LowStock = await LowStockAlertsAsync(),
            PaymentReminders = await CustomerRemindersAsync(today, reminderCutoff),
            PurchaseReminders = await SupplierRemindersAsync(today, reminderCutoff),
            ChequeReminders = await ChequeRemindersAsync(today, reminderCutoff),
            RecentInvoices = await _db.SalesInvoices.AsNoTracking()
                .Include(i => i.Buyer)
                .Include(i => i.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .Take(8)
                .ToListAsync()
        };
        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    private static string DueStatus(DateTime dueDate, DateTime today) =>
        dueDate < today ? "Overdue" : dueDate == today ? "Due today" : "Due tomorrow";

    private async Task<List<ProductStockAlert>> LowStockAlertsAsync()
    {
        var products = await _db.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync();

        // Same on-hand rule as InvoiceService.OwnedStockOnHandAsync, but grouped so it's two
        // queries in total instead of two per product.
        var purchased = await _db.OwnedPurchases.AsNoTracking()
            .GroupBy(p => p.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(p => p.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Qty);
        var sold = await _db.SalesInvoices.AsNoTracking()
            .Where(s => s.StockType == StockType.OwnedStock)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(s => s.QuantitySold) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Qty);

        return products
            .Select(p => new { Product = p, OnHand = purchased.GetValueOrDefault(p.Id) - sold.GetValueOrDefault(p.Id) })
            .Where(x => x.OnHand <= x.Product.MinimumStockAlertQty)
            .Select(x => new ProductStockAlert
            {
                ProductName = x.Product.Name,
                OnHand = x.OnHand,
                AlertQty = x.Product.MinimumStockAlertQty
            })
            .ToList();
    }

    private async Task<List<PaymentReminder>> CustomerRemindersAsync(DateTime today, DateTime cutoff)
    {
        var creditInvoices = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Buyer)
            .Where(i => i.PaymentType == PaymentType.Credit
                && !i.ReminderDismissed
                && i.DueDate != null
                && i.DueDate.Value.Date <= cutoff)
            .ToListAsync();

        var invoiceIds = creditInvoices.Select(i => i.Id).ToList();
        var paid = await _db.LedgerEntries.AsNoTracking()
            .Where(v => v.SalesInvoiceId != null && invoiceIds.Contains(v.SalesInvoiceId.Value) && v.Type == LedgerEntryType.PaymentReceived)
            .GroupBy(v => v.SalesInvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Amount = g.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Amount);

        return creditInvoices
            .Select(inv => new { Invoice = inv, AmountDue = inv.GrandTotalAmount - paid.GetValueOrDefault(inv.Id) })
            .Where(x => x.AmountDue > 0)
            .Select(x => new PaymentReminder
            {
                InvoiceId = x.Invoice.Id,
                InvoiceNumber = x.Invoice.InvoiceNumber,
                BuyerName = x.Invoice.Buyer?.FullName ?? "-",
                DueDate = x.Invoice.DueDate!.Value,
                AmountDue = x.AmountDue,
                IsOverdue = x.Invoice.DueDate!.Value.Date < today,
                Status = DueStatus(x.Invoice.DueDate!.Value.Date, today)
            })
            .OrderBy(r => r.DueDate)
            .ToList();
    }

    private async Task<List<PurchasePaymentReminder>> SupplierRemindersAsync(DateTime today, DateTime cutoff)
    {
        var creditPurchases = await _db.OwnedPurchases.AsNoTracking()
            .Include(p => p.Vendor)
            .Where(p => p.PaymentType == PaymentType.Credit
                && !p.ReminderDismissed
                && p.DueDate != null
                && p.DueDate.Value.Date <= cutoff)
            .ToListAsync();

        var outstanding = await _ledger.GetPurchaseOutstandingAsync(creditPurchases.Select(p => p.Id));

        return creditPurchases
            .Select(p => new { Purchase = p, AmountDue = outstanding.GetValueOrDefault(p.Id) })
            .Where(x => x.AmountDue > 0)
            .Select(x => new PurchasePaymentReminder
            {
                PurchaseId = x.Purchase.Id,
                GrnNumber = x.Purchase.GrnNumber,
                VendorName = x.Purchase.Vendor?.FullName ?? "-",
                DueDate = x.Purchase.DueDate!.Value,
                AmountDue = x.AmountDue,
                IsOverdue = x.Purchase.DueDate!.Value.Date < today,
                Status = DueStatus(x.Purchase.DueDate!.Value.Date, today)
            })
            .OrderBy(r => r.DueDate)
            .ToList();
    }

    private async Task<List<ChequeReminder>> ChequeRemindersAsync(DateTime today, DateTime cutoff)
    {
        var chequeEntries = await _db.LedgerEntries.AsNoTracking()
            .Include(e => e.Party)
            .Where(e => e.Mode == PaymentMode.Cheque
                && e.ChequeDate != null
                && e.ChequeDate.Value.Date <= cutoff)
            .ToListAsync();

        return chequeEntries
            .Select(e => new ChequeReminder
            {
                LedgerEntryId = e.Id,
                LedgerNumber = e.LedgerNumber,
                PartyName = e.Party?.FullName ?? "-",
                ChequeDate = e.ChequeDate!.Value,
                Amount = e.Amount,
                IsOverdue = e.ChequeDate!.Value.Date < today,
                Status = DueStatus(e.ChequeDate!.Value.Date, today)
            })
            .OrderBy(r => r.ChequeDate)
            .ToList();
    }
}
