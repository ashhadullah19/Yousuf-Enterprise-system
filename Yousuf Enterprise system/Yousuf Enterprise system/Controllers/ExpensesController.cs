using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
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

    public async Task<IActionResult> Index(string? q, DateTime? from, DateTime? to)
    {
        var query = _db.Expenses.AsNoTracking().Include(e => e.BankAccount).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(e =>
                e.ExpenseNumber.Contains(q)
                || e.Category.Contains(q)
                || (e.Description != null && e.Description.Contains(q)));
        }

        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

        ViewBag.Query = q;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id).ToListAsync();
        ViewBag.Total = expenses.Sum(e => e.Amount);
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
            _db.BankTransactions.Add(new BankTransaction
            {
                BankAccountId = expense.BankAccountId.Value,
                TransactionDate = expense.ExpenseDate,
                Type = TransactionType.Withdrawal,
                Amount = expense.Amount,
                ReferenceNumber = expense.ExpenseNumber,
                Remarks = $"Expense {expense.ExpenseNumber} ({expense.Category})"
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

        await FillListsAsync();
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
            await FillListsAsync();
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

    private async Task FillListsAsync()
    {
        ViewBag.BankAccounts = new SelectList(
            await _db.BankAccounts.OrderBy(b => b.BankName).ToListAsync(),
            "Id", "AccountTitle");
    }
}
