using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public class PartyLedgerLine
{
    public DateTime Date { get; set; }
    public string Document { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HeadType Head { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public interface ILedgerService
{
    Task<IReadOnlyList<PartyLedgerLine>> GetPartyLedgerAsync(int partyId, DateTime? from = null, DateTime? to = null);
    Task<(decimal Receivables, decimal Payables)> GetCompanyTotalsAsync();
    Task<decimal> TodayCashFlowAsync();
    Task<decimal> OwnedStockValueAsync();
}

public class LedgerService : ILedgerService
{
    private readonly ApplicationDbContext _db;

    public LedgerService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PartyLedgerLine>> GetPartyLedgerAsync(int partyId, DateTime? from = null, DateTime? to = null)
    {
        var lines = new List<PartyLedgerLine>();

        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.BuyerId == partyId)
            .ToListAsync();

        foreach (var invoice in invoices)
        {
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Sales invoice (receivable)",
                Head = HeadType.DirectProductHead,
                Debit = invoice.GrandTotalAmount
            });
        }

        var consignments = await _db.SalesInvoices.AsNoTracking()
    .Include(i => i.ConsignmentReceipt)
    .Where(i => i.StockType == StockType.ConsignmentStock
                && i.ConsignmentReceipt != null
                && i.ConsignmentReceipt.StockOwnerId == partyId)
    .ToListAsync();

        foreach (var invoice in consignments)
        {
            var commissionPct = invoice.ConsignmentReceipt!.AgreedCommissionPercentage;
            var commissionRevenue = Math.Round(invoice.SubtotalAmount * commissionPct / 100m, 2);
            var stockOwnerPayable = invoice.SubtotalAmount - commissionRevenue;

            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Consignment net payable to stock owner",
                Head = HeadType.DirectProductHead,
                Credit = stockOwnerPayable
            });
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Commission income (company)",
                Head = HeadType.CommissionHead,
                Credit = commissionRevenue
            });
        }

        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => p.VendorId == partyId)
            .ToListAsync();

        foreach (var purchase in purchases)
        {
            lines.Add(new PartyLedgerLine
            {
                Date = purchase.PurchaseDate,
                Document = purchase.GrnNumber,
                Description = "Owned stock purchase (payable)",
                Head = HeadType.DirectProductHead,
                Credit = purchase.GrandTotalAmount
            });
        }

        var vouchers = await _db.FinancialVouchers.AsNoTracking()
            .Where(v => v.PartyId == partyId && (v.Type != VoucherType.ContraAdjustment || v.ApprovedByAdmin))
            .ToListAsync();

        foreach (var voucher in vouchers)
        {
            var line = new PartyLedgerLine
            {
                Date = voucher.VoucherDate,
                Document = voucher.VoucherNumber,
                Description = $"{voucher.Type} via {voucher.Mode}",
                Head = voucher.Head
            };

            switch (voucher.Type)
            {
                case VoucherType.PaymentReceived:
                    line.Credit = voucher.Amount;
                    break;
                case VoucherType.PaymentPaid:
                    line.Debit = voucher.Amount;
                    break;
                case VoucherType.ContraAdjustment:
                    line.Debit = voucher.Amount;
                    line.Credit = voucher.Amount;
                    line.Description = "Contra netting (mutual settlement)";
                    break;
            }

            lines.Add(line);
        }

        IEnumerable<PartyLedgerLine> query = lines;
        if (from.HasValue)
        {
            query = query.Where(l => l.Date >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.Date <= to.Value.Date);
        }

        return query.OrderBy(l => l.Date).ThenBy(l => l.Document).ToList();
    }

    public async Task<(decimal Receivables, decimal Payables)> GetCompanyTotalsAsync()
    {
        var parties = await _db.Parties.AsNoTracking().Select(p => p.Id).ToListAsync();
        decimal receivables = 0;
        decimal payables = 0;

        foreach (var partyId in parties)
        {
            var ledger = await GetPartyLedgerAsync(partyId);
            var net = ledger.Where(l => l.Head == HeadType.DirectProductHead)
                .Sum(l => l.Debit - l.Credit);
            if (net > 0)
            {
                receivables += net;
            }
            else
            {
                payables += Math.Abs(net);
            }
        }

        return (receivables, payables);
    }

    public async Task<decimal> TodayCashFlowAsync()
    {
        var today = DateTime.Today;
        var received = await _db.FinancialVouchers
            .Where(v => v.VoucherDate == today && v.Type == VoucherType.PaymentReceived)
            .SumAsync(v => (decimal?)v.Amount) ?? 0;
        var paid = await _db.FinancialVouchers
            .Where(v => v.VoucherDate == today && v.Type == VoucherType.PaymentPaid)
            .SumAsync(v => (decimal?)v.Amount) ?? 0;
        return received - paid;
    }

    public async Task<decimal> OwnedStockValueAsync()
    {
        var purchases = await _db.OwnedPurchases.AsNoTracking().ToListAsync();
        var sold = await _db.SalesInvoices.AsNoTracking()
            .Where(s => s.StockType == StockType.OwnedStock)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.QuantitySold) })
            .ToListAsync();

        decimal value = 0;
        foreach (var group in purchases.GroupBy(p => p.ProductId))
        {
            var qty = group.Sum(p => p.Quantity);
            var cost = group.Sum(p => p.GrandTotalAmount);
            var avg = qty == 0 ? 0 : cost / qty;
            var soldQty = sold.FirstOrDefault(s => s.ProductId == group.Key)?.Qty ?? 0;
            value += Math.Max(0, qty - soldQty) * avg;
        }

        return Math.Round(value, 2);
    }
}
