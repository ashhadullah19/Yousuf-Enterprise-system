using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Ledgers)]
public class LedgerEntriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly ILedgerService _ledger;

    public LedgerEntriesController(ApplicationDbContext db, IDocumentNumberService numbers, ILedgerService ledger)
    {
        _db = db;
        _numbers = numbers;
        _ledger = ledger;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 25)
    {
        var query = _db.LedgerEntries.AsNoTracking()
            .Include(v => v.Party)
            .Include(v => v.SalesInvoice)
            .Include(v => v.OwnedPurchase)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(v =>
                v.LedgerNumber.Contains(q)
                || (v.Party != null && v.Party.FullName.Contains(q))
                || (v.ReferenceNumber != null && v.ReferenceNumber.Contains(q))
                || (v.OwnedPurchase != null && v.OwnedPurchase.GrnNumber.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderByDescending(v => v.LedgerDate).ThenByDescending(v => v.Id).ToPagedResultAsync(page, pageSize));
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
            // No money actually moves for a contra — it's a paper netting, not a cash/cheque/bank
            // transfer — so Mode is forced regardless of what the (hidden, for this type) form field posted.
            entry.Mode = PaymentMode.Cash;
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
                    ModelState.AddModelError(nameof(entry.Amount), $"Amount exceeds the outstanding due of {outstanding:N0} on this invoice.");
                }
            }
        }

        // Settling a purchase only makes sense for money paid out to its vendor.
        if (entry.OwnedPurchaseId.HasValue && entry.Type != LedgerEntryType.PaymentPaid)
        {
            entry.OwnedPurchaseId = null;
        }

        if (entry.OwnedPurchaseId.HasValue)
        {
            var purchase = await _db.OwnedPurchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entry.OwnedPurchaseId.Value);
            if (purchase is null || purchase.VendorId != entry.PartyId)
            {
                ModelState.AddModelError(nameof(entry.OwnedPurchaseId), "Selected purchase does not belong to this party.");
            }
            else
            {
                entry.Head = HeadType.DirectProductHead;
                var outstanding = (await _ledger.GetPurchaseOutstandingAsync(new[] { purchase.Id })).GetValueOrDefault(purchase.Id);
                if (entry.Amount > outstanding)
                {
                    ModelState.AddModelError(nameof(entry.Amount), $"Amount exceeds the outstanding balance of {outstanding:N0} on this purchase.");
                }
            }
        }

        // Bank account only makes sense for an actual bank movement, and a cheque hasn't
        // cleared yet — only an online transfer posts a bank transaction automatically.
        if (entry.Mode != PaymentMode.OnlineBankTransfer)
        {
            entry.BankAccountId = null;
        }
        if (entry.Mode != PaymentMode.Cheque)
        {
            entry.ChequeDate = null;
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", entry);
        }

        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync();

        if (entry.Mode == PaymentMode.OnlineBankTransfer && entry.BankAccountId.HasValue
            && entry.Type is LedgerEntryType.PaymentReceived or LedgerEntryType.PaymentPaid)
        {
            _db.BankTransactions.Add(new BankTransaction
            {
                BankAccountId = entry.BankAccountId.Value,
                TransactionDate = entry.LedgerDate,
                Type = entry.Type == LedgerEntryType.PaymentReceived ? TransactionType.Deposit : TransactionType.Withdrawal,
                Amount = entry.Amount,
                ReferenceNumber = entry.LedgerNumber,
                Remarks = $"Ledger entry {entry.LedgerNumber}"
            });
            await _db.SaveChangesAsync();
        }

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
                result.Add(new { id = invoice.Id, label = $"{invoice.InvoiceNumber} — due {due:N0}" });
            }
        }

        return Json(result);
    }

    // Live list of a vendor's purchases that still have an outstanding balance, for the
    // "Against Purchase" picker on the ledger entry form once a party is selected.
    [HttpGet]
    public async Task<IActionResult> OutstandingPurchases(int partyId)
    {
        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => p.VendorId == partyId)
            .OrderBy(p => p.PurchaseDate).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.GrnNumber })
            .ToListAsync();
        var outstanding = await _ledger.GetPurchaseOutstandingAsync(purchases.Select(p => p.Id));

        return Json(purchases
            .Where(p => outstanding.GetValueOrDefault(p.Id) > 0)
            .Select(p => new { id = p.Id, label = $"{p.GrnNumber} — due {outstanding[p.Id]:N0}" }));
    }

    // Marks a cheque as cleared at the bank so it stops nagging on the dashboard — the payment
    // itself was already recorded the moment this ledger entry was posted, so this doesn't touch
    // any balance, it only affects whether the reminder keeps showing.
    [ModulePermission(Modules.Ledgers, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearCheque(int id)
    {
        var entry = await _db.LedgerEntries.FindAsync(id);
        if (entry is not null)
        {
            entry.ChequeCleared = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Index", "Home");
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

    public async Task<IActionResult> Pdf(int id)
    {
        var entry = await _db.LedgerEntries.AsNoTracking()
            .Include(e => e.Party)
            .Include(e => e.SalesInvoice)
            .Include(e => e.OwnedPurchase)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();

        var isPending = entry.Type == LedgerEntryType.ContraAdjustment && !entry.ApprovedByAdmin;
        var against = entry.SalesInvoice?.InvoiceNumber ?? entry.OwnedPurchase?.GrnNumber;

        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(settings.CompanyName.ToUpperInvariant()).FontSize(20).Bold();
                    if (!string.IsNullOrWhiteSpace(settings.Address))
                    {
                        col.Item().AlignCenter().PaddingTop(1).Text(settings.Address).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    }
                    col.Item().AlignCenter().PaddingTop(8).Text("LEDGER ENTRY VOUCHER").FontSize(13).Bold().Underline();

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Text(entry.LedgerNumber).FontSize(13).Bold();
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Date: {entry.LedgerDate:dd-MMM-yyyy}");
                            var statusColor = isPending ? QuestPDF.Helpers.Colors.Amber.Darken2 : QuestPDF.Helpers.Colors.Green.Darken2;
                            c.Item().Text($"Status: {(isPending ? "Pending approval" : "Posted")}").FontColor(statusColor).SemiBold();
                        });
                    });
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    void DetailRow(string label, string? value)
                    {
                        if (string.IsNullOrWhiteSpace(value)) return;
                        col.Item().PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Row(row =>
                        {
                            row.ConstantItem(140).Text(label).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                            row.RelativeItem().Text(value).SemiBold();
                        });
                    }

                    DetailRow("Party", entry.Party?.FullName);
                    DetailRow("Type", entry.Type.GetDisplayName());
                    DetailRow("Head", entry.Head.GetDisplayName());
                    DetailRow("Mode", entry.Mode.GetDisplayName());
                    DetailRow(entry.SalesInvoice is not null ? "Against Invoice" : "Against Purchase", against);
                    DetailRow("Reference", entry.ReferenceNumber);
                    DetailRow("Bank", entry.BankName);
                    if (entry.ChequeDate.HasValue)
                    {
                        DetailRow("Cheque Date", entry.ChequeDate.Value.ToString("dd-MMM-yyyy") + (entry.ChequeCleared ? " (Cleared)" : " (Pending)"));
                    }
                    DetailRow("Remarks", entry.Remarks);

                    col.Item().PaddingTop(16).AlignRight().Width(240).Border(1).BorderColor(QuestPDF.Helpers.Colors.Black)
                        .Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("Amount:").Bold();
                        row.ConstantItem(110).AlignRight().Text($"Rs {entry.Amount:N0}").Bold().FontSize(14);
                    });

                    col.Item().PaddingTop(40).Text("Signature:");
                });
            });
        }).GeneratePdf();

        return File(bytes, "application/pdf", $"{entry.LedgerNumber}.pdf");
    }

    private async Task FillListsAsync()
    {
        ViewBag.Parties = new SelectList(await _db.Parties.OrderBy(p => p.FullName).ToListAsync(), "Id", "FullName");
        ViewBag.BankAccounts = new SelectList(await _db.BankAccounts.OrderBy(b => b.BankName).ToListAsync(), "Id", "AccountTitle");
    }
}
