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

        var model = new DashboardViewModel
        {
            OwnedStockValue = await _ledger.OwnedStockValueAsync(),
            Receivables = totals.Receivables,
            Payables = totals.Payables,
            TodayCashFlow = await _ledger.TodayCashFlowAsync(),
            LowStock = alerts,
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
