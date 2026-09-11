using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IExportService
{
    Task<byte[]> ExportPartiesAsync();
    Task<byte[]> ExportProductsAsync();
    Task<byte[]> ExportInvoicesAsync();
    Task<byte[]> ExportStockAsync();
    Task<(int Parties, int Products)> ImportMastersAsync(Stream excelStream);
    Task<int> ImportPartiesAsync(Stream excelStream);
    Task<int> ImportProductsAsync(Stream excelStream);
    byte[] PartiesTemplate();
    byte[] ProductsTemplate();
}

public class ExportService : IExportService
{
    private readonly ApplicationDbContext _db;

    public ExportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportPartiesAsync()
    {
        var rows = await _db.Parties.AsNoTracking().OrderBy(p => p.FullName).ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Parties");
        sheet.Cell(1, 1).InsertTable(rows.Select(p => new
        {
            p.Id,
            p.FullName,
            Type = p.Type.ToString(),
            p.FatherName,
            p.PrimaryContact,
            p.Ntn,
            p.BankName,
            p.AccountNumber
        }));
        return Save(workbook);
    }

    public async Task<byte[]> ExportProductsAsync()
    {
        var rows = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Products");
        sheet.Cell(1, 1).InsertTable(rows.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            Uom = p.Uom.ToString(),
            p.MinimumStockAlertQty,
            p.IsActive
        }));
        return Save(workbook);
    }

    public async Task<byte[]> ExportInvoicesAsync()
    {
        var rows = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Buyer)
            .Include(i => i.Product)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoices");
        sheet.Cell(1, 1).InsertTable(rows.Select(i => new
        {
            i.InvoiceNumber,
            i.InvoiceDate,
            Buyer = i.Buyer?.FullName,
            Product = i.Product?.Name,
            i.QuantitySold,
            i.GrandTotalAmount,
            i.ApplyGst
        }));
        return Save(workbook);
    }

    public async Task<byte[]> ExportStockAsync()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Stock");
        sheet.Cell(1, 1).Value = "Product";
        sheet.Cell(1, 2).Value = "Owned Qty";
        sheet.Cell(1, 3).Value = "Consignment Qty";
        var row = 2;
        foreach (var product in products)
        {
            var ownedIn = await _db.OwnedPurchases.Where(p => p.ProductId == product.Id).SumAsync(p => (decimal?)p.Quantity) ?? 0;
            var ownedOut = await _db.SalesInvoices.Where(s => s.ProductId == product.Id && s.StockType == StockType.OwnedStock).SumAsync(s => (decimal?)s.QuantitySold) ?? 0;
            var consIn = await _db.ConsignmentReceipts.Where(c => c.ProductId == product.Id).SumAsync(c => (decimal?)c.ReceivedQuantity) ?? 0;
            var consOut = await _db.SalesInvoices.Where(s => s.ProductId == product.Id && s.StockType == StockType.ConsignmentStock).SumAsync(s => (decimal?)s.QuantitySold) ?? 0;
            sheet.Cell(row, 1).Value = product.Name;
            sheet.Cell(row, 2).Value = ownedIn - ownedOut;
            sheet.Cell(row, 3).Value = consIn - consOut;
            row++;
        }

        return Save(workbook);
    }

    public async Task<(int Parties, int Products)> ImportMastersAsync(Stream excelStream)
    {
        using var workbook = new XLWorkbook(excelStream);
        var partiesAdded = 0;
        var productsAdded = 0;

        if (workbook.TryGetWorksheet("Parties", out var partySheet))
        {
            var used = partySheet.RangeUsed();
            if (used is not null)
            {
                foreach (var row in used.RowsUsed().Skip(1))
                {
                    var name = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (await _db.Parties.AnyAsync(p => p.FullName == name))
                    {
                        continue;
                    }

                    Enum.TryParse<PartyType>(row.Cell(2).GetString(), true, out var type);
                    if (type == 0)
                    {
                        type = PartyType.Both;
                    }

                    _db.Parties.Add(new Party
                    {
                        FullName = name,
                        Type = type,
                        PrimaryContact = row.Cell(3).GetString(),
                    });
                    partiesAdded++;
                }
            }
        }

        if (workbook.TryGetWorksheet("Products", out var productSheet))
        {
            var used = productSheet.RangeUsed();
            if (used is not null)
            {
                foreach (var row in used.RowsUsed().Skip(1))
                {
                    var name = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(name) || await _db.Products.AnyAsync(p => p.Name == name))
                    {
                        continue;
                    }

                    Enum.TryParse<UnitOfMeasure>(row.Cell(2).GetString(), true, out var uom);
                    if (uom == 0)
                    {
                        uom = UnitOfMeasure.KG;
                    }

                    _db.Products.Add(new Product
                    {
                        Name = name,
                        Uom = uom,
                        Description = row.Cell(3).GetString(),
                        IsActive = true
                    });
                    productsAdded++;
                }
            }
        }

        await _db.SaveChangesAsync();
        return (partiesAdded, productsAdded);
    }

    // Reads the first worksheet's header row and matches columns by name (case-insensitive),
    // so each page's import doesn't depend on a fixed sheet name like the combined importer does.
    private static Dictionary<string, int> ReadHeaders(ClosedXML.Excel.IXLWorksheet sheet)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return columns;
        }

        foreach (var cell in used.FirstRow().Cells())
        {
            var text = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                columns[text] = cell.Address.ColumnNumber;
            }
        }

        return columns;
    }

    public async Task<int> ImportPartiesAsync(Stream excelStream)
    {
        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheet(1);
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return 0;
        }

        var columns = ReadHeaders(sheet);
        string? Get(IXLRangeRow row, string col) => columns.TryGetValue(col, out var idx) ? row.Cell(idx).GetString().Trim() : null;

        var added = 0;
        foreach (var row in used.RowsUsed().Skip(1))
        {
            var name = Get(row, "FullName");
            if (string.IsNullOrWhiteSpace(name) || await _db.Parties.AnyAsync(p => p.FullName == name))
            {
                continue;
            }

            Enum.TryParse<PartyType>(Get(row, "Type"), true, out var type);
            if (type == 0)
            {
                type = PartyType.Both;
            }

            var bankName = Get(row, "BankName");

            _db.Parties.Add(new Party
            {
                FullName = name,
                Type = type,
                FatherName = Get(row, "FatherName"),
                PrimaryContact = Get(row, "PrimaryContact") ?? string.Empty,
                SecondaryContact = Get(row, "SecondaryContact"),
                BusinessAddress = Get(row, "BusinessAddress"),
                Ntn = Get(row, "Ntn"),
                StrnSbr = Get(row, "StrnSbr"),
                HasBankDetails = !string.IsNullOrWhiteSpace(bankName),
                BankName = bankName,
                AccountTitle = Get(row, "AccountTitle"),
                AccountNumber = Get(row, "AccountNumber"),
                Iban = Get(row, "Iban"),
                BranchCode = Get(row, "BranchCode")
            });
            added++;
        }

        await _db.SaveChangesAsync();
        return added;
    }

    public async Task<int> ImportProductsAsync(Stream excelStream)
    {
        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheet(1);
        var used = sheet.RangeUsed();
        if (used is null)
        {
            return 0;
        }

        var columns = ReadHeaders(sheet);
        string? Get(IXLRangeRow row, string col) => columns.TryGetValue(col, out var idx) ? row.Cell(idx).GetString().Trim() : null;

        var added = 0;
        foreach (var row in used.RowsUsed().Skip(1))
        {
            var name = Get(row, "Name");
            if (string.IsNullOrWhiteSpace(name) || await _db.Products.AnyAsync(p => p.Name == name))
            {
                continue;
            }

            Enum.TryParse<UnitOfMeasure>(Get(row, "Uom"), true, out var uom);
            if (uom == 0)
            {
                uom = UnitOfMeasure.KG;
            }

            decimal.TryParse(Get(row, "MinimumStockAlertQty"), out var minQty);
            var isActiveText = Get(row, "IsActive");
            var isActive = string.IsNullOrWhiteSpace(isActiveText) || !string.Equals(isActiveText, "false", StringComparison.OrdinalIgnoreCase);

            _db.Products.Add(new Product
            {
                Name = name,
                Description = Get(row, "Description"),
                Uom = uom,
                MinimumStockAlertQty = minQty,
                IsActive = isActive
            });
            added++;
        }

        await _db.SaveChangesAsync();
        return added;
    }

    public byte[] PartiesTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Parties");
        var headers = new[] { "FullName", "Type", "FatherName", "PrimaryContact", "SecondaryContact", "BusinessAddress", "Ntn", "StrnSbr", "BankName", "AccountTitle", "AccountNumber", "Iban", "BranchCode" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        return Save(workbook);
    }

    public byte[] ProductsTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Products");
        var headers = new[] { "Name", "Description", "Uom", "MinimumStockAlertQty", "IsActive" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        return Save(workbook);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
