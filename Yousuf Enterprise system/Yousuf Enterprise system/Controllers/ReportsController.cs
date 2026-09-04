using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
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

    public IActionResult Index() => View();

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
        return View(lines);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            var result = await _export.ImportMastersAsync(stream);
            TempData["Message"] = $"Imported {result.Parties} parties and {result.Products} products.";
        }

        return RedirectToAction(nameof(Index));
    }
}
