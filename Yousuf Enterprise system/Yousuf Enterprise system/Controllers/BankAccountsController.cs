using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class BankAccountsController : Controller
{
    private readonly ApplicationDbContext _db;

    public BankAccountsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.BankAccounts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.AccountTitle.Contains(q) || (p.AccountNumber != null && p.AccountNumber.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderBy(p => p.BankName).ToListAsync());
    }

    public IActionResult Create() => View("Form", new BankAccount());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BankAccount bankAccount)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", bankAccount);
        }

        _db.BankAccounts.Add(bankAccount);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.BankAccounts.FindAsync(id);
        return product is null ? NotFound() : View("Form", product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BankAccount product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", product);
        }

        _db.Update(product);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.BankAccounts.FindAsync(id);
        if (product is not null)
        {
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
