using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IDocumentNumberService
{
    Task<string> NextAsync(string prefix, Func<ApplicationDbContext, IQueryable<string>> selector);
}

public class DocumentNumberService : IDocumentNumberService
{
    private readonly ApplicationDbContext _db;

    public DocumentNumberService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(string prefix, Func<ApplicationDbContext, IQueryable<string>> selector)
    {
        var year = DateTime.Today.Year;
        var stamp = $"{prefix}-{year}-";
        var last = await selector(_db)
            .Where(n => n.StartsWith(stamp))
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last is not null)
        {
            var tail = last[stamp.Length..];
            if (int.TryParse(tail, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return $"{stamp}{next:D5}";
    }
}
