using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Expenses)]
public class ExpensesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;

    public ExpensesController(ApplicationDbContext db, IDocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<IActionResult> Index(string? q, int? expenseTypeId, DateTime? from, DateTime? to, int page = 1, int pageSize = 25)
    {
        var query = _db.Expenses.AsNoTracking().Include(e => e.BankAccount).Include(e => e.ExpenseType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(e =>
                e.ExpenseNumber.Contains(q)
                || e.ExpenseType!.Name.Contains(q)
                || (e.Description != null && e.Description.Contains(q)));
        }

        if (expenseTypeId.HasValue) query = query.Where(e => e.ExpenseTypeId == expenseTypeId.Value);
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

        ViewBag.ExpenseTypes = await _db.ExpenseTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        ViewBag.ExpenseTypeId = expenseTypeId;
        ViewBag.Query = q;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        ViewBag.Total = await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id).ToPagedResultAsync(page, pageSize);
        return View(expenses);
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        return View("Form", new Expense
        {
            ExpenseNumber = await _numbers.NextAsync("EXP", db => db.Expenses.Select(e => e.ExpenseNumber)),
            ExpenseDate = DateTime.Today
        });
    }

    [ModulePermission(Modules.Expenses, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Expense expense)
    {
        if (expense.Mode != PaymentMode.OnlineBankTransfer)
        {
            expense.BankAccountId = null;
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", expense);
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        if (expense.Mode == PaymentMode.OnlineBankTransfer && expense.BankAccountId.HasValue)
        {
            var typeName = await _db.ExpenseTypes.Where(t => t.Id == expense.ExpenseTypeId).Select(t => t.Name).FirstOrDefaultAsync();
            _db.BankTransactions.Add(new BankTransaction
            {
                BankAccountId = expense.BankAccountId.Value,
                TransactionDate = expense.ExpenseDate,
                Type = TransactionType.Withdrawal,
                Amount = expense.Amount,
                ReferenceNumber = expense.ExpenseNumber,
                Remarks = $"Expense {expense.ExpenseNumber} ({typeName})"
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense is null)
        {
            return NotFound();
        }

        await FillListsAsync(expense.ExpenseTypeId);
        return View("Form", expense);
    }

    [ModulePermission(Modules.Expenses, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Expense expense)
    {
        if (id != expense.Id)
        {
            return BadRequest();
        }

        if (expense.Mode != PaymentMode.OnlineBankTransfer)
        {
            expense.BankAccountId = null;
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync(expense.ExpenseTypeId);
            return View("Form", expense);
        }

        _db.Update(expense);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense is not null)
        {
            _db.Expenses.Remove(expense);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // Inactive types are hidden from new entries, but the type an existing expense already uses stays selectable.
    private async Task FillListsAsync(int currentTypeId = 0)
    {
        ViewBag.BankAccounts = new SelectList(
            await _db.BankAccounts.OrderBy(b => b.BankName).ToListAsync(),
            "Id", "AccountTitle");
        ViewBag.ExpenseTypes = new SelectList(
            await _db.ExpenseTypes.AsNoTracking()
                .Where(t => t.IsActive || t.Id == currentTypeId)
                .OrderBy(t => t.Name)
                .ToListAsync(),
            "Id", "Name");
    }
}
