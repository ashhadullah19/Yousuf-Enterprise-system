using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledger;
    private readonly IInvoiceService _invoices;

    public HomeController(ApplicationDbContext db, ILedgerService ledger, IInvoiceService invoices)
    {
        _db = db;
        _ledger = ledger;
        _invoices = invoices;
    }

    public async Task<IActionResult> Index()
    {
        var totals = await _ledger.GetCompanyTotalsAsync();
        var products = await _db.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync();
        var alerts = new List<ProductStockAlert>();
        foreach (var product in products)
        {
            var onHand = await _invoices.OwnedStockOnHandAsync(product.Id);
            if (onHand <= product.MinimumStockAlertQty)
            {
                alerts.Add(new ProductStockAlert
                {
                    ProductName = product.Name,
                    OnHand = onHand,
                    AlertQty = product.MinimumStockAlertQty
                });
            }
        }

        // Due tomorrow (1 day before) through overdue — matches "1 day before" reminder rule.
        var today = DateTime.Today;
        var reminderWindowStart = today.AddDays(1);

        var creditInvoices = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Buyer)
            .Where(i => i.PaymentType == PaymentType.Credit
                && i.DueDate != null
                && i.DueDate.Value.Date <= reminderWindowStart)
            .ToListAsync();

        var paymentReminders = new List<PaymentReminder>();
        foreach (var inv in creditInvoices)
        {
            var paid = await _db.BankTransactions
                .Where(t => t.SalesInvoiceId == inv.Id && t.Type == TransactionType.Deposit)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var amountDue = inv.GrandTotalAmount - paid;
            if (amountDue <= 0) continue; // fully paid — no reminder needed

            paymentReminders.Add(new PaymentReminder
            {
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                BuyerName = inv.Buyer?.FullName ?? "-",
                DueDate = inv.DueDate!.Value,
                AmountDue = amountDue,
                IsOverdue = inv.DueDate.Value.Date < today
            });
        }
        paymentReminders = paymentReminders.OrderBy(r => r.DueDate).ToList();

        var model = new DashboardViewModel
        {
            OwnedStockValue = await _ledger.OwnedStockValueAsync(),
            Receivables = totals.Receivables,
            Payables = totals.Payables,
            TodayCashFlow = await _ledger.TodayCashFlowAsync(),
            LowStock = alerts,
            PaymentReminders = paymentReminders,
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
}
