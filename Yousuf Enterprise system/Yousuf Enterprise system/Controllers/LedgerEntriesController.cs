using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Ledgers)]
public class LedgerEntriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;

    public LedgerEntriesController(ApplicationDbContext db, IDocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.LedgerEntries.AsNoTracking().Include(v => v.Party).Include(v => v.SalesInvoice).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(v =>
                v.LedgerNumber.Contains(q)
                || (v.Party != null && v.Party.FullName.Contains(q))
                || (v.ReferenceNumber != null && v.ReferenceNumber.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderByDescending(v => v.LedgerDate).ThenByDescending(v => v.Id).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        return View("Form", new LedgerEntry
        {
            LedgerNumber = await _numbers.NextAsync("LED", db => db.LedgerEntries.Select(v => v.LedgerNumber)),
            LedgerDate = DateTime.Today
        });
    }

    [ModulePermission(Modules.Ledgers, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LedgerEntry entry)
    {
        if (entry.Type == LedgerEntryType.ContraAdjustment)
        {
            // Contra netting only makes sense for a party's Direct Product (dues) balance —
            // it nets a vendor payable against a buyer receivable for the same party. Applying
            // it to Commission would post equal Debit+Credit to that head, corrupting the
            // Accrued/Received split there, so it's always forced to Direct Product.
            entry.Head = HeadType.DirectProductHead;
            entry.ApprovedByAdmin = User.IsInRole(AppRoles.SuperAdmin);
            var party = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entry.PartyId);
            if (party is null || party.Type != PartyType.Both)
            {
                ModelState.AddModelError(string.Empty, "Contra netting is only allowed for parties marked as Both (vendor and buyer).");
            }
        }
        else
        {
            entry.ApprovedByAdmin = true;
        }

        // Invoice settlement only makes sense for money received against a receivable.
        if (entry.SalesInvoiceId.HasValue && entry.Type != LedgerEntryType.PaymentReceived)
        {
            entry.SalesInvoiceId = null;
        }

        if (entry.SalesInvoiceId.HasValue)
        {
            var invoice = await _db.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == entry.SalesInvoiceId.Value);
            if (invoice is null || invoice.BuyerId != entry.PartyId)
            {
                ModelState.AddModelError(nameof(entry.SalesInvoiceId), "Selected invoice does not belong to this party.");
            }
            else
            {
                // Settling an invoice is always a Direct Product (dues) matter, never commission.
                entry.Head = HeadType.DirectProductHead;

                var alreadyPaid = await _db.LedgerEntries.AsNoTracking()
                    .Where(v => v.SalesInvoiceId == invoice.Id && v.Type == LedgerEntryType.PaymentReceived)
                    .SumAsync(v => (decimal?)v.Amount) ?? 0m;
                var outstanding = invoice.GrandTotalAmount - alreadyPaid;
                if (entry.Amount > outstanding)
                {
                    ModelState.AddModelError(nameof(entry.Amount), $"Amount exceeds the outstanding due of {outstanding:N2} on this invoice.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", entry);
        }

        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // Live list of a party's invoices that still have an outstanding due, for the
    // "Against Invoice" picker on the ledger entry form once a party is selected.
    [HttpGet]
    public async Task<IActionResult> OutstandingInvoices(int partyId)
    {
        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.BuyerId == partyId)
            .ToListAsync();

        var result = new List<object>();
        foreach (var invoice in invoices)
        {
            var paid = await _db.LedgerEntries.AsNoTracking()
                .Where(v => v.SalesInvoiceId == invoice.Id && v.Type == LedgerEntryType.PaymentReceived)
                .SumAsync(v => (decimal?)v.Amount) ?? 0m;
            var due = invoice.GrandTotalAmount - paid;
            if (due > 0)
            {
                result.Add(new { id = invoice.Id, label = $"{invoice.InvoiceNumber} — due {due:N2}" });
            }
        }

        return Json(result);
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var entry = await _db.LedgerEntries.FindAsync(id);
        if (entry is null)
        {
            return NotFound();
        }

        entry.ApprovedByAdmin = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync()
    {
        ViewBag.Parties = new SelectList(await _db.Parties.OrderBy(p => p.FullName).ToListAsync(), "Id", "FullName");
    }
}
