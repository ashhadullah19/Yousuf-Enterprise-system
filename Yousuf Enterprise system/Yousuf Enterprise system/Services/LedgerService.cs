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

public class PartyCommissionBalance
{
    public decimal Dues { get; set; }
    public decimal CommissionAccrued { get; set; }
    public decimal CommissionReceived { get; set; }
    public decimal CommissionReceivable => CommissionAccrued - CommissionReceived;
}

public interface ILedgerService
{
    Task<IReadOnlyList<PartyLedgerLine>> GetPartyLedgerAsync(int partyId, DateTime? from = null, DateTime? to = null);
    Task<(decimal Receivables, decimal Payables)> GetCompanyTotalsAsync();
    Task<decimal> TodayCashFlowAsync();
    Task<decimal> OwnedStockValueAsync();
    Task<Dictionary<int, PartyCommissionBalance>> GetAllPartyBalancesAsync();
    Task<decimal> GetTotalCommissionAsync();
    Task<decimal> GetConsignmentCommissionRevenueAsync();
    Task<decimal> GetTotalProfitAsync();
}

public class LedgerService : ILedgerService
{
    private readonly ApplicationDbContext _db;
    private readonly IInvoiceService _invoices;

    public LedgerService(ApplicationDbContext db, IInvoiceService invoices)
    {
        _db = db;
        _invoices = invoices;
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

            // Only the net payable belongs on the stock owner's own ledger — the commission
            // itself is company revenue (not money owed to/from this party), so it is tracked
            // company-wide via GetConsignmentCommissionRevenueAsync instead, not on this line.
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Consignment net payable to stock owner",
                Head = HeadType.DirectProductHead,
                Credit = stockOwnerPayable
            });
        }

        // Agent commission: accrues on this party's ledger (Commission head) whenever they're
        // set as the earning agent on an invoice — separate from the invoice's own buyer/dues.
        var agentInvoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.AgentId == partyId && i.AgentCommissionAmount > 0)
            .ToListAsync();

        foreach (var invoice in agentInvoices)
        {
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = $"Commission accrued on invoice {invoice.InvoiceNumber}",
                Head = HeadType.CommissionHead,
                Credit = invoice.AgentCommissionAmount
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

        // Owned-stock commission comes out of the vendor's payable, not the company's profit
        // (see InvoiceService.CalculateAsync). Every owned-stock sale with a commission gets
        // resolved via FIFO to whichever vendor(s) actually supplied that stock, and this
        // vendor's share of it is booked here as a debit reducing what they're owed.
        if (purchases.Count > 0)
        {
            var commissionedSales = await _db.SalesInvoices.AsNoTracking()
                .Where(s => s.StockType == StockType.OwnedStock
                    && s.AgentCommissionAmount > 0
                    && purchases.Select(p => p.ProductId).Distinct().Contains(s.ProductId))
                .ToListAsync();

            foreach (var sale in commissionedSales)
            {
                var allocation = await _invoices.GetSaleVendorAllocationAsync(sale);
                var vendorShare = allocation.FirstOrDefault(a => a.VendorId == partyId);
                if (vendorShare is not null && vendorShare.Cost > 0)
                {
                    var commissionShare = Math.Round(vendorShare.Cost * sale.AgentCommissionPercentage / 100m, 2);
                    if (commissionShare > 0)
                    {
                        lines.Add(new PartyLedgerLine
                        {
                            Date = sale.InvoiceDate,
                            Document = sale.InvoiceNumber,
                            Description = $"Commission deducted from purchase cost (invoice {sale.InvoiceNumber})",
                            Head = HeadType.DirectProductHead,
                            Debit = commissionShare
                        });
                    }
                }
            }
        }

        var ledgerEntries = await _db.LedgerEntries.AsNoTracking()
            .Where(v => v.PartyId == partyId && (v.Type != LedgerEntryType.ContraAdjustment || v.ApprovedByAdmin))
            .ToListAsync();

        foreach (var entry in ledgerEntries)
        {
            var line = new PartyLedgerLine
            {
                Date = entry.LedgerDate,
                Document = entry.LedgerNumber,
                Description = $"{entry.Type.GetDisplayName()} via {entry.Mode.GetDisplayName()}",
                Head = entry.Head
            };

            switch (entry.Type)
            {
                case LedgerEntryType.PaymentReceived:
                    line.Credit = entry.Amount;
                    break;
                case LedgerEntryType.PaymentPaid:
                    line.Debit = entry.Amount;
                    break;
                case LedgerEntryType.ContraAdjustment:
                    line.Debit = entry.Amount;
                    line.Credit = entry.Amount;
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
        var received = await _db.LedgerEntries
            .Where(v => v.LedgerDate == today && v.Type == LedgerEntryType.PaymentReceived)
            .SumAsync(v => (decimal?)v.Amount) ?? 0;
        var paid = await _db.LedgerEntries
            .Where(v => v.LedgerDate == today && v.Type == LedgerEntryType.PaymentPaid)
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

    // Splits each party's ledger balance by Head: Dues (DirectProductHead, net) and Commission,
    // with commission further split into Accrued (total ever earned) and Received (total ever
    // paid out to them) so "receivable" (Accrued - Received) is visible, not just a blended net.
    public async Task<Dictionary<int, PartyCommissionBalance>> GetAllPartyBalancesAsync()
    {
        var parties = await _db.Parties.AsNoTracking().Select(p => p.Id).ToListAsync();
        var result = new Dictionary<int, PartyCommissionBalance>();

        foreach (var partyId in parties)
        {
            var ledger = await GetPartyLedgerAsync(partyId);
            var dues = ledger.Where(l => l.Head == HeadType.DirectProductHead).Sum(l => l.Debit - l.Credit);
            var commissionLines = ledger.Where(l => l.Head == HeadType.CommissionHead).ToList();
            result[partyId] = new PartyCommissionBalance
            {
                Dues = Math.Round(dues, 2),
                CommissionAccrued = Math.Round(commissionLines.Sum(l => l.Credit), 2),
                CommissionReceived = Math.Round(commissionLines.Sum(l => l.Debit), 2)
            };
        }

        return result;
    }

    // Sum of every party's CommissionAccrued, so this always reconciles with the per-party
    // Receivable+Received figures shown on the Parties page — one source of truth, not a
    // separate calculation that can drift out of sync with what the parties add up to.
    public async Task<decimal> GetTotalCommissionAsync()
    {
        var balances = await GetAllPartyBalancesAsync();
        return Math.Round(balances.Values.Sum(b => b.CommissionAccrued), 2);
    }

    // Company's own revenue from reselling consignment stock (the cut kept before paying the
    // stock owner their net). Not attributed to any party's ledger — nobody owes/is owed this.
    public async Task<decimal> GetConsignmentCommissionRevenueAsync()
    {
        var consignments = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.ConsignmentReceipt)
            .Where(i => i.StockType == StockType.ConsignmentStock && i.ConsignmentReceipt != null)
            .ToListAsync();

        var total = consignments.Sum(i => Math.Round(i.SubtotalAmount * i.ConsignmentReceipt!.AgreedCommissionPercentage / 100m, 2));
        return Math.Round(total, 2);
    }

    public async Task<decimal> GetTotalProfitAsync()
    {
        return Math.Round(await _db.SalesInvoices.AsNoTracking().SumAsync(i => (decimal?)i.ProfitAmount) ?? 0, 2);
    }
}
