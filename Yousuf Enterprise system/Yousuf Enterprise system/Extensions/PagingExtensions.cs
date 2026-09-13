using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Extensions;

public static class PagingExtensions
{
    public static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };
    public const int DefaultPageSize = 25;

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int? page, int? pageSize)
    {
        var size = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value) ? pageSize.Value : DefaultPageSize;
        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)size);
        var current = Math.Clamp(page ?? 1, 1, totalPages);

        var items = await query.Skip((current - 1) * size).Take(size).ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            Page = current,
            PageSize = size,
            TotalCount = totalCount
        };
    }

    // In-memory variant for sources that can't be paged in the database (e.g. filters
    // that require client-side evaluation, like case-insensitive Contains on Identity users).
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int? page, int? pageSize)
    {
        var size = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value) ? pageSize.Value : DefaultPageSize;
        var list = source as IReadOnlyCollection<T> ?? source.ToList();
        var totalCount = list.Count;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)size);
        var current = Math.Clamp(page ?? 1, 1, totalPages);

        var items = list.Skip((current - 1) * size).Take(size).ToList();

        return new PagedResult<T>
        {
            Items = items,
            Page = current,
            PageSize = size,
            TotalCount = totalCount
        };
    }
}
