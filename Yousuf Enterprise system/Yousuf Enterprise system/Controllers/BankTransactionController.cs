using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.BankTransactions)]
public class BankTransactionController : Controller
{
    private readonly ApplicationDbContext _db;
    public BankTransactionController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? bankAccountId, string? q, int page = 1, int pageSize = 25)
    {
        var accounts = await _db.BankAccounts.AsNoTracking()
            .OrderBy(b => b.BankName).ThenBy(b => b.AccountTitle)
            .ToListAsync();

        var selected = bankAccountId.HasValue ? accounts.FirstOrDefault(a => a.Id == bankAccountId.Value) : null;

        ViewBag.BankAccounts = accounts
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.BankName} - {a.AccountTitle} ({a.AccountNumber})",
                Selected = a.Id == selected?.Id
            })
            .ToList();
        ViewBag.SelectedAccount = selected;
        ViewBag.Query = q;

        // Nothing is listed until a bank is picked, so each list (and its search) is per account.
        if (selected is null)
        {
            return View(new PagedResult<BankTransaction>());
        }

        var query = _db.BankTransactions.AsNoTracking()
            .Where(t => t.BankAccountId == selected.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(t =>
                (t.ReferenceNumber != null && t.ReferenceNumber.Contains(q)) ||
                (t.Remarks != null && t.Remarks.Contains(q)));
        }

        return View(await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .ToPagedResultAsync(page, pageSize));
    }

    private async Task PopulateBankAccountsAsync(int? selectedId = null)
    {
        ViewBag.BankAccounts = new SelectList(
            await _db.BankAccounts.OrderBy(b => b.BankName).ToListAsync(),
            "Id", "AccountTitle", selectedId);
    }

    public async Task<IActionResult> Create(int? bankAccountId)
    {
        await PopulateBankAccountsAsync(bankAccountId);
        return View("Form", new BankTransaction { BankAccountId = bankAccountId ?? 0 });
    }

    [ModulePermission(Modules.BankTransactions, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BankTransaction transaction)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBankAccountsAsync(transaction.BankAccountId);
            return View("Form", transaction);
        }
        _db.BankTransactions.Add(transaction);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { bankAccountId = transaction.BankAccountId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var transaction = await _db.BankTransactions.FindAsync(id);
        if (transaction is null)
        {
            return NotFound();
        }
        await PopulateBankAccountsAsync(transaction.BankAccountId);
        return View("Form", transaction);
    }

    [ModulePermission(Modules.BankTransactions, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BankTransaction transaction)
    {
        if (id != transaction.Id)
        {
            return BadRequest();
        }
        if (!ModelState.IsValid)
        {
            await PopulateBankAccountsAsync(transaction.BankAccountId);
            return View("Form", transaction);
        }
        _db.Update(transaction);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { bankAccountId = transaction.BankAccountId });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var transaction = await _db.BankTransactions.FindAsync(id);
        if (transaction is null)
        {
            return RedirectToAction(nameof(Index));
        }

        _db.BankTransactions.Remove(transaction);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { bankAccountId = transaction.BankAccountId });
    }
    public async Task<IActionResult> Statement(int bankAccountId, DateTime? from, DateTime? to)
    {
        var account = await _db.BankAccounts.FindAsync(bankAccountId);
        if (account is null)
        {
            return NotFound();
        }

        var query = _db.BankTransactions.AsNoTracking()
            .Where(t => t.BankAccountId == bankAccountId);

        if (from.HasValue) query = query.Where(t => t.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(t => t.TransactionDate <= to.Value);

        // Compute running balance chronologically (oldest first), then reverse for display
        var chronological = await query
            .OrderBy(t => t.TransactionDate).ThenBy(t => t.Id)
            .ToListAsync();

        decimal running = 0;
        var rows = new List<StatementRow>();
        foreach (var t in chronological)
        {
            running += t.Type == TransactionType.Deposit ? t.Amount : -t.Amount;
            rows.Add(new StatementRow
            {
                Transaction = t,
                RunningBalance = running
            });
        }

        rows.Reverse(); // recent first

        ViewBag.Account = account;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.ClosingBalance = running;

        return View(rows);
    }

    public async Task<IActionResult> StatementPdf(int bankAccountId, DateTime? from, DateTime? to)
    {
        var account = await _db.BankAccounts.FindAsync(bankAccountId);
        if (account is null) return NotFound();

        var query = _db.BankTransactions.AsNoTracking()
            .Where(t => t.BankAccountId == bankAccountId);
        if (from.HasValue) query = query.Where(t => t.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(t => t.TransactionDate <= to.Value);

        var chronological = await query.OrderBy(t => t.TransactionDate).ThenBy(t => t.Id).ToListAsync();

        decimal running = 0;
        var rows = chronological.Select(t =>
        {
            running += t.Type == TransactionType.Deposit ? t.Amount : -t.Amount;
            return (t, Balance: running);
        }).Reverse().ToList();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"{account.BankName} - {account.AccountTitle} ({account.AccountNumber})")
                    .SemiBold().FontSize(14);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(3);
                        c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Date").SemiBold();
                        h.Cell().Text("Type").SemiBold();
                        h.Cell().Text("Reference").SemiBold();
                        h.Cell().Text("Deposit").SemiBold();
                        h.Cell().Text("Withdrawal").SemiBold();
                        h.Cell().Text("Balance").SemiBold();
                    });

                    foreach (var (t, balance) in rows)
                    {
                        table.Cell().Text(t.TransactionDate.ToString("dd-MMM-yyyy"));
                        table.Cell().Text(t.Type.ToString());
                        table.Cell().Text(t.ReferenceNumber ?? "");
                        table.Cell().Text(t.Type == TransactionType.Deposit ? t.Amount.ToString("N2") : "");
                        table.Cell().Text(t.Type == TransactionType.Withdrawal ? t.Amount.ToString("N2") : "");
                        table.Cell().Text(balance.ToString("N2"));
                    }
                });

                page.Footer().AlignRight().Text($"Closing Balance: {running:N2}").SemiBold();
            });
        }).GeneratePdf();

        var fileName = $"Statement_{account.AccountTitle}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}