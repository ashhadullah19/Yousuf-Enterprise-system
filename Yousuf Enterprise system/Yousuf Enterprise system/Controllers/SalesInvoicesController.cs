using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class SalesInvoicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IInvoiceService _invoices;
    private readonly IInvoicePdfService _pdf;

    public SalesInvoicesController(
        ApplicationDbContext db,
        IDocumentNumberService numbers,
        IInvoiceService invoices,
        IInvoicePdfService pdf)
    {
        _db = db;
        _numbers = numbers;
        _invoices = invoices;
        _pdf = pdf;
    }

    public async Task<IActionResult> Index(string? q, bool? gst)
    {
        var query = _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Buyer)
            .Include(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(i =>
                i.InvoiceNumber.Contains(q)
                || (i.Buyer != null && i.Buyer.FullName.Contains(q))
                || (i.Product != null && i.Product.Name.Contains(q)));
        }

        if (gst.HasValue)
        {
            query = query.Where(i => i.ApplyGst == gst.Value);
        }

        ViewBag.Query = q;
        ViewBag.Gst = gst;
        return View(await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
        return View("Form", new SalesInvoice
        {
            InvoiceNumber = await _numbers.NextAsync("INV", db => db.SalesInvoices.Select(i => i.InvoiceNumber)),
            InvoiceDate = DateTime.Today,
            GstPercentage = settings.DefaultGstPercentage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SalesInvoice invoice)
    {
        try
        {
            await _invoices.ApplyAndValidateAsync(invoice);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", invoice);
        }

        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.SalesInvoices
            .Include(i => i.Buyer)
            .Include(i => i.Product)
            .Include(i => i.ConsignmentReceipt)
            .ThenInclude(c => c!.StockOwner)
            .FirstOrDefaultAsync(i => i.Id == id);
        return invoice is null ? NotFound() : View(invoice);
    }

    public async Task<IActionResult> Pdf(int id)
    {
        var invoice = await _db.SalesInvoices
            .Include(i => i.Buyer)
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
        var bytes = _pdf.Build(settings, invoice);
        return File(bytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    private async Task FillListsAsync()
    {
        ViewBag.Buyers = new SelectList(
            await _db.Parties.Where(p => p.Type == PartyType.Buyer || p.Type == PartyType.Both).OrderBy(p => p.FullName).ToListAsync(),
            "Id", "FullName");
        ViewBag.Products = new SelectList(
            await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
        ViewBag.Receipts = new SelectList(
            await _db.ConsignmentReceipts.Include(c => c.StockOwner).Include(c => c.Product)
                .OrderByDescending(c => c.ReceiptDate)
                .Select(c => new { c.Id, Label = "#" + c.Id + " " + c.StockOwner!.FullName + " / " + c.Product!.Name })
                .ToListAsync(),
            "Id", "Label");
    }
}
