using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class ConsignmentReceiptsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ConsignmentReceiptsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.ConsignmentReceipts.AsNoTracking()
            .Include(c => c.StockOwner)
            .Include(c => c.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c =>
                c.Id.ToString() == q
                || (c.StockOwner != null && c.StockOwner.FullName.Contains(q))
                || (c.Product != null && c.Product.Name.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderByDescending(c => c.ReceiptDate).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        return View("Form", new ConsignmentReceipt { ReceiptDate = DateTime.Today, AgreedCommissionPercentage = 20 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConsignmentReceipt receipt)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", receipt);
        }

        _db.ConsignmentReceipts.Add(receipt);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var receipt = await _db.ConsignmentReceipts.FindAsync(id);
        if (receipt is null)
        {
            return NotFound();
        }

        await FillListsAsync();
        return View("Form", receipt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ConsignmentReceipt receipt)
    {
        if (id != receipt.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", receipt);
        }

        _db.Update(receipt);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var receipt = await _db.ConsignmentReceipts.FindAsync(id);
        if (receipt is not null)
        {
            receipt.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync()
    {
        ViewBag.Owners = new SelectList(
            await _db.Parties.Where(p => p.Type == PartyType.Vendor || p.Type == PartyType.Both).OrderBy(p => p.FullName).ToListAsync(),
            "Id", "FullName");
        ViewBag.Products = new SelectList(
            await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }
}
