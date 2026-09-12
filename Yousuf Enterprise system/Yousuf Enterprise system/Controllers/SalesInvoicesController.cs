using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Sales)]
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

    public async Task<IActionResult> Index(string? q, bool? gst, bool? overdueOnly)
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

        if (overdueOnly == true)
        {
            var today = DateTime.Today;
            query = query.Where(i => i.PaymentType == PaymentType.Credit
                && i.DueDate != null && i.DueDate.Value.Date < today);
        }

        ViewBag.Query = q;
        ViewBag.Gst = gst;
        ViewBag.OverdueOnly = overdueOnly;
        var invoices = await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync();

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        ViewBag.AmountPaid = await _db.LedgerEntries.AsNoTracking()
            .Where(v => v.SalesInvoiceId != null && invoiceIds.Contains(v.SalesInvoiceId.Value) && v.Type == LedgerEntryType.PaymentReceived)
            .GroupBy(v => v.SalesInvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid);

        return View(invoices);
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
        return View("Form", new SalesInvoice
        {
            InvoiceNumber = await _numbers.NextAsync("FT", db => db.SalesInvoices.Select(i => i.InvoiceNumber), yearDigits: 2, seqDigits: 2),
            InvoiceDate = DateTime.Today,
            GstPercentage = settings.DefaultGstPercentage
        });
    }

    [ModulePermission(Modules.Sales, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SalesInvoice invoice)
    {
        // Conditional requirements based on PaymentType
        if (invoice.PaymentType == PaymentType.Online && invoice.BankAccountId is null)
        {
            ModelState.AddModelError(nameof(invoice.BankAccountId), "Bank account is required for online payments.");
        }
        if (invoice.PaymentType == PaymentType.Credit && invoice.DueDate is null)
        {
            ModelState.AddModelError(nameof(invoice.DueDate), "Due date is required when payment is on credit.");
        }
        if (invoice.PaymentType != PaymentType.Online)
        {
            invoice.BankAccountId = null; // don't persist stale selection
        }
        if (invoice.PaymentType != PaymentType.Credit)
        {
            invoice.DueDate = null;
        }

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

        // Cash/Online sales are paid at the point of sale — auto-settle so they don't sit as
        // open receivables (that's what a Credit sale is for). Online additionally posts the
        // matching bank deposit so it actually shows up in that account's transactions.
        if (invoice.PaymentType is PaymentType.Cash or PaymentType.Online)
        {
            var settlement = new LedgerEntry
            {
                LedgerNumber = await _numbers.NextAsync("LED", db => db.LedgerEntries.Select(e => e.LedgerNumber)),
                LedgerDate = invoice.InvoiceDate,
                PartyId = invoice.BuyerId,
                SalesInvoiceId = invoice.Id,
                Type = LedgerEntryType.PaymentReceived,
                Head = HeadType.DirectProductHead,
                Mode = invoice.PaymentType == PaymentType.Online ? PaymentMode.OnlineBankTransfer : PaymentMode.Cash,
                Amount = invoice.GrandTotalAmount,
                ApprovedByAdmin = true,
                Remarks = $"Auto-settled at sale ({invoice.PaymentType})"
            };
            _db.LedgerEntries.Add(settlement);

            if (invoice.PaymentType == PaymentType.Online && invoice.BankAccountId.HasValue)
            {
                _db.BankTransactions.Add(new BankTransaction
                {
                    BankAccountId = invoice.BankAccountId.Value,
                    TransactionDate = invoice.InvoiceDate,
                    Type = TransactionType.Deposit,
                    Amount = invoice.GrandTotalAmount,
                    ReferenceNumber = invoice.InvoiceNumber,
                    Remarks = $"Sales invoice {invoice.InvoiceNumber}"
                });
            }

            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [ModulePermission(Modules.Sales, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReminder(int id)
    {
        var invoice = await _db.SalesInvoices.FindAsync(id);
        if (invoice is not null)
        {
            invoice.ReminderDismissed = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.SalesInvoices
            .Include(i => i.Buyer)
            .Include(i => i.Product)
            .Include(i => i.BankAccount)
            .Include(i => i.Agent)
            .Include(i => i.ConsignmentReceipt)
            .ThenInclude(c => c!.StockOwner)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null) return NotFound();

        // Settlement now happens via ledger entries (Add Ledger Entry), not Bank Transaction.
        ViewBag.AmountPaid = await _db.LedgerEntries
            .Where(v => v.SalesInvoiceId == id && v.Type == LedgerEntryType.PaymentReceived)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        return View(invoice);
    }

    // Live stock lookup used by the Sales Invoice form to show on-hand qty / remaining
    // consignment qty and (for owned stock) which vendors have historically supplied it.
    [HttpGet]
    public async Task<IActionResult> StockInfo(int productId, StockType stockType, int? consignmentReceiptId)
    {
        if (stockType == StockType.ConsignmentStock)
        {
            if (!consignmentReceiptId.HasValue)
            {
                return Json(new { onHand = 0m, uom = "", vendors = Array.Empty<object>() });
            }

            var receipt = await _db.ConsignmentReceipts.AsNoTracking()
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Id == consignmentReceiptId.Value);
            var remaining = await _invoices.ConsignmentRemainingAsync(consignmentReceiptId.Value);
            return Json(new { onHand = remaining, uom = receipt?.Product?.Uom.ToString() ?? "", vendors = Array.Empty<object>() });
        }

        var onHandQty = await _invoices.OwnedStockOnHandAsync(productId);
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
        var byVendor = await _invoices.OwnedStockByVendorAsync(productId);
        var vendors = byVendor.Select(v => new { name = v.VendorName, qty = v.RemainingQty });

        return Json(new { onHand = onHandQty, uom = product?.Uom.ToString() ?? "", vendors });
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

        // Owned-stock product list: one option per vendor who still has remaining supply,
        // e.g. "Vendor A CHIA SEED(50.00)", so the buyer can see/choose which vendor's
        // stock they're effectively drawing from (informational — the sale itself still
        // only records Product+Qty, cost is the same weighted average regardless of pick).
        var activeProducts = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var ownedOptions = new List<object>();
        foreach (var product in activeProducts)
        {
            var byVendor = await _invoices.OwnedStockByVendorAsync(product.Id);
            foreach (var (vendorName, qty) in byVendor)
            {
                ownedOptions.Add(new { product.Id, Label = $"{vendorName} {product.Name}({qty:N2})" });
            }
        }
        ViewBag.Products = new SelectList(ownedOptions, "Id", "Label");
        ViewBag.Receipts = new SelectList(
            await _db.ConsignmentReceipts.Include(c => c.StockOwner).Include(c => c.Product)
                .OrderByDescending(c => c.ReceiptDate)
                .Select(c => new { c.Id, Label = "#" + c.Id + " " + c.StockOwner!.FullName + " / " + c.Product!.Name })
                .ToListAsync(),
            "Id", "Label");
        ViewBag.BankAccounts = new SelectList(
            await _db.BankAccounts.OrderBy(b => b.BankName).ToListAsync(),
            "Id", "AccountTitle");
        ViewBag.Agents = new SelectList(
            await _db.Parties.OrderBy(p => p.FullName).ToListAsync(),
            "Id", "FullName");
    }
}