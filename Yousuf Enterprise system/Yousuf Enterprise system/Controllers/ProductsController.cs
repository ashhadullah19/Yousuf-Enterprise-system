using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Products)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IInvoiceService _invoices;
    private readonly IExportService _export;

    public ProductsController(ApplicationDbContext db, IInvoiceService invoices, IExportService export)
    {
        _db = db;
        _invoices = invoices;
        _export = export;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.Name.Contains(q) || (p.Description != null && p.Description.Contains(q)));
        }

        ViewBag.Query = q;
        var products = await query.OrderBy(p => p.Name).ToListAsync();

        var onHand = new Dictionary<int, decimal>();
        foreach (var product in products)
        {
            onHand[product.Id] = await _invoices.OwnedStockOnHandAsync(product.Id);
        }
        ViewBag.OnHand = onHand;

        return View(products);
    }

    [ModulePermission(Modules.Products, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            var added = await _export.ImportProductsAsync(stream);
            TempData["Message"] = $"Imported {added} product(s).";
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult DownloadTemplate()
    {
        var bytes = _export.ProductsTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products-template.xlsx");
    }

    public IActionResult Create() => View("Form", new Product());

    [ModulePermission(Modules.Products, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (await _db.Products.AnyAsync(p => p.Name == product.Name))
        {
            ModelState.AddModelError(nameof(product.Name), "A product with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", product);
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        return product is null ? NotFound() : View("Form", product);
    }

    [ModulePermission(Modules.Products, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (await _db.Products.AnyAsync(p => p.Name == product.Name && p.Id != id))
        {
            ModelState.AddModelError(nameof(product.Name), "A product with this name already exists.");
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
        var product = await _db.Products.FindAsync(id);
        if (product is not null)
        {
            product.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
