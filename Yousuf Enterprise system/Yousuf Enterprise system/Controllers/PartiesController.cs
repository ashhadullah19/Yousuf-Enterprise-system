using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class PartiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public PartiesController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(string? q, string sort = "name")
    {
        var query = _db.Parties.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p =>
                p.FullName.Contains(q)
                || (p.FatherName != null && p.FatherName.Contains(q))
                || (p.Cnic != null && p.Cnic.Contains(q))
                || (p.PrimaryContact != null && p.PrimaryContact.Contains(q))
                || (p.Ntn != null && p.Ntn.Contains(q))
                || (p.AccountNumber != null && p.AccountNumber.Contains(q)));
        }

        query = sort switch
        {
            "type" => query.OrderBy(p => p.Type).ThenBy(p => p.FullName),
            _ => query.OrderBy(p => p.FullName)
        };

        ViewBag.Query = q;
        return View(await query.ToListAsync());
    }

    public IActionResult Create() => View("Form", new Party { Type = PartyType.Both });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Party party, IFormFile? cnicFront, IFormFile? cnicBack, IFormFile? cheque, IFormFile? taxCert)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", party);
        }

        _db.Parties.Add(party);
        await _db.SaveChangesAsync();
        await SaveAttachmentsAsync(party, cnicFront, cnicBack, cheque, taxCert);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var party = await _db.Parties.FindAsync(id);
        return party is null ? NotFound() : View("Form", party);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Party party, IFormFile? cnicFront, IFormFile? cnicBack, IFormFile? cheque, IFormFile? taxCert)
    {
        if (id != party.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", party);
        }

        _db.Update(party);
        await SaveAttachmentsAsync(party, cnicFront, cnicBack, cheque, taxCert);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var party = await _db.Parties.FindAsync(id);
        if (party is not null)
        {
            party.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task SaveAttachmentsAsync(Party party, IFormFile? cnicFront, IFormFile? cnicBack, IFormFile? cheque, IFormFile? taxCert)
    {
        var dir = Path.Combine(_env.WebRootPath, "uploads", "parties", party.Id.ToString());
        Directory.CreateDirectory(dir);

        party.CnicFrontPath = await SaveFileAsync(cnicFront, dir, "cnic-front") ?? party.CnicFrontPath;
        party.CnicBackPath = await SaveFileAsync(cnicBack, dir, "cnic-back") ?? party.CnicBackPath;
        party.CanceledChequePath = await SaveFileAsync(cheque, dir, "cheque") ?? party.CanceledChequePath;
        party.TaxCertificatePath = await SaveFileAsync(taxCert, dir, "tax") ?? party.TaxCertificatePath;
    }

    private static async Task<string?> SaveFileAsync(IFormFile? file, string dir, string name)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var ext = Path.GetExtension(file.FileName);
        var path = Path.Combine(dir, name + ext);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream);
        return Path.Combine("uploads", "parties", Path.GetFileName(dir), name + ext).Replace('\\', '/');
    }
}
