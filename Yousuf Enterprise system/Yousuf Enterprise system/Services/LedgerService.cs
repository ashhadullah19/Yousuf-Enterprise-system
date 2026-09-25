using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

// Which of a party's two dues balances a line moves. A party can be both a buyer and a vendor,
// and what they owe us must not silently offset what we owe them — otherwise receiving their
// payment makes our payable to them appear to grow. Only an approved contra nets the two.
public enum DuesSide
{
    None,
    Receivable,
    Payable
}

public class PartyLedgerLine
{
    public DateTime Date { get; set; }
    public string Document { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HeadType Head { get; set; }
    public DuesSide Side { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    // Same-day ordering: source documents (invoices, purchases) before the payments and contras
    // that settle them, so running balances never dip negative just because of document numbering.
    public int SortPriority { get; set; }
}

public class PartyCommissionBalance
{
    // Signed per side: positive ReceivableBalance = they owe us, positive PayableBalance = we owe
    // them. A negative side means it was over-settled (an advance).
    public decimal ReceivableBalance { get; set; }
    public decimal PayableBalance { get; set; }

    // An over-settled side belongs on the other one: an advance they paid us is money we owe back.
    public decimal Receivable => Math.Max(ReceivableBalance, 0) + Math.Max(-PayableBalance, 0);
    public decimal Payable => Math.Max(PayableBalance, 0) + Math.Max(-ReceivableBalance, 0);

    public decimal CommissionAccrued { get; set; }
    public decimal CommissionReceived { get; set; }
    public decimal CommissionReceivable => CommissionAccrued - CommissionReceived;

    public static PartyCommissionBalance FromLedger(IEnumerable<PartyLedgerLine> lines)
    {
        var all = lines as IReadOnlyCollection<PartyLedgerLine> ?? lines.ToList();
        var commission = all.Where(l => l.Head == HeadType.CommissionHead).ToList();
        return new PartyCommissionBalance
        {
            ReceivableBalance = Math.Round(all.Where(l => l.Side == DuesSide.Receivable).Sum(l => l.Debit - l.Credit), 2),
            PayableBalance = Math.Round(all.Where(l => l.Side == DuesSide.Payable).Sum(l => l.Credit - l.Debit), 2),
            CommissionAccrued = Math.Round(commission.Sum(l => l.Credit), 2),
            CommissionReceived = Math.Round(commission.Sum(l => l.Debit), 2)
        };
    }
}

// One party with commission still owed back to the company, for the dashboard reminder.
public class CommissionReminderRow
{
    public int PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public decimal Outstanding { get; set; }
    public DateTime OldestAccrualDate { get; set; }
    public int PendingEntryCount { get; set; }
}

public interface ILedgerService
{
    Task<IReadOnlyList<PartyLedgerLine>> GetPartyLedgerAsync(int partyId, DateTime? from = null, DateTime? to = null);
    Task<decimal> TodayCashFlowAsync();
    Task<decimal> OwnedStockValueAsync();
    Task<Dictionary<int, PartyCommissionBalance>> GetAllPartyBalancesAsync();
    Task<List<CommissionReminderRow>> GetCommissionRemindersAsync();
    Task<Dictionary<int, decimal>> GetPurchaseOutstandingAsync(IEnumerable<int> purchaseIds);
    Task<decimal> GetConsignmentCommissionRevenueAsync();
    Task<decimal> GetTotalProfitAsync();
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
        var data = await LoadLedgerDataAsync();
        IEnumerable<PartyLedgerLine> query = BuildPartyLines(data, partyId);
        if (from.HasValue)
        {
            query = query.Where(l => l.Date >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.Date <= to.Value.Date);
        }

        return query.OrderBy(l => l.Date).ThenBy(l => l.SortPriority).ThenBy(l => l.Document).ToList();
    }

    // Per party: dues split into separate Receivable and Payable sides, plus commission split into
    // Accrued (total ever earned) and Received (total ever paid out to them). All ledger data is
    // loaded once and every party is built from it in memory — previously each party cost its own
    // round of queries (plus two more per older commissioned sale), which is slow on a remote database.
    public async Task<Dictionary<int, PartyCommissionBalance>> GetAllPartyBalancesAsync()
    {
        var parties = await _db.Parties.AsNoTracking().Select(p => p.Id).ToListAsync();
        var data = await LoadLedgerDataAsync();
        return parties.ToDictionary(id => id, id => PartyCommissionBalance.FromLedger(BuildPartyLines(data, id)));
    }

    // Every party with commission still outstanding (accrued but not yet collected back from
    // them) — owned-stock vendors and consignment/legacy agents alike, since both post to the
    // same Commission head. "Oldest" is the earliest still-unsettled accrual, used to show how
    // long it's been pending; it assumes receipts settle the oldest accruals first (no per-line
    // link exists between a receipt and the accrual it settles, so this is the same FIFO
    // assumption the rest of the ledger already makes for owned-stock cost).
    public async Task<List<CommissionReminderRow>> GetCommissionRemindersAsync()
    {
        var parties = await _db.Parties.AsNoTracking()
            .Select(p => new { p.Id, p.FullName })
            .ToListAsync();
        var data = await LoadLedgerDataAsync();

        var rows = new List<CommissionReminderRow>();
        foreach (var party in parties)
        {
            var lines = BuildPartyLines(data, party.Id);
            var balance = PartyCommissionBalance.FromLedger(lines);
            if (balance.CommissionReceivable <= 0)
            {
                continue;
            }

            var accruals = lines.Where(l => l.Head == HeadType.CommissionHead && l.Credit > 0)
                .OrderBy(l => l.Date)
                .ToList();
            if (accruals.Count == 0)
            {
                continue;
            }

            // How many receipts back from the end are already settled, so the oldest STILL
            // outstanding accrual (not just the oldest ever) is what drives the "pending since".
            var alreadyReceived = balance.CommissionReceived;
            var oldestOutstanding = accruals[0].Date;
            foreach (var accrual in accruals)
            {
                if (alreadyReceived >= accrual.Credit)
                {
                    alreadyReceived -= accrual.Credit;
                    continue;
                }

                oldestOutstanding = accrual.Date;
                break;
            }

            rows.Add(new CommissionReminderRow
            {
                PartyId = party.Id,
                PartyName = party.FullName,
                Outstanding = balance.CommissionReceivable,
                OldestAccrualDate = oldestOutstanding,
                PendingEntryCount = accruals.Count
            });
        }

        return rows.OrderBy(r => r.OldestAccrualDate).ToList();
    }

    // What's still owed on each purchase: its total, less payments recorded against it. Commission
    // is NOT deducted here — the vendor is paid in full, and commission is collected back from
    // them separately (see the Commission head lines in BuildPartyLines), so it must never reduce
    // what shows as owed on the purchase itself.
    public async Task<Dictionary<int, decimal>> GetPurchaseOutstandingAsync(IEnumerable<int> purchaseIds)
    {
        var ids = purchaseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var totals = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.GrandTotalAmount })
            .ToListAsync();

        var paid = await _db.LedgerEntries.AsNoTracking()
            .Where(e => e.OwnedPurchaseId != null && ids.Contains(e.OwnedPurchaseId.Value) && e.Type == LedgerEntryType.PaymentPaid)
            .GroupBy(e => e.OwnedPurchaseId!.Value)
            .Select(g => new { Id = g.Key, Amount = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount);

        return totals.ToDictionary(
            t => t.Id,
            t => Math.Round(t.GrandTotalAmount - paid.GetValueOrDefault(t.Id), 2));
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
        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .OrderBy(p => p.PurchaseDate).ThenBy(p => p.Id)
            .Select(p => new { p.ProductId, p.Quantity, p.RatePerUnit })
            .ToListAsync();
        var sold = await _db.SalesInvoices.AsNoTracking()
            .Where(s => s.StockType == StockType.OwnedStock)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.QuantitySold) })
            .ToListAsync();

        decimal value = 0;
        foreach (var group in purchases.GroupBy(p => p.ProductId))
        {
            // Deplete this product's lots oldest-first against what's been sold (same FIFO order
            // AvailableLotsAsync uses), then value whatever remains in each lot at that lot's own
            // purchase rate — not a blended average, since lots from different vendors/dates can
            // carry different rates.
            var toConsume = sold.FirstOrDefault(s => s.ProductId == group.Key)?.Qty ?? 0;
            foreach (var lot in group)
            {
                var consumed = Math.Min(lot.Quantity, toConsume);
                var remaining = lot.Quantity - consumed;
                toConsume -= consumed;
                value += remaining * lot.RatePerUnit;
            }
        }

        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
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

    private sealed class InvoiceRow
    {
        public int Id { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public DateTime InvoiceDate { get; init; }
        public int BuyerId { get; init; }
        public int? AgentId { get; init; }
        public int ProductId { get; init; }
        public StockType StockType { get; init; }
        public decimal QuantitySold { get; init; }
        public decimal SubtotalAmount { get; init; }
        public decimal GrandTotalAmount { get; init; }
        public decimal AgentCommissionAmount { get; init; }
        public decimal AgentCommissionPercentage { get; init; }
        public bool HasAllocations { get; init; }
        public int? StockOwnerId { get; init; }
        public decimal? AgreedCommissionPercentage { get; init; }
    }

    private sealed class PurchaseRow
    {
        public int Id { get; init; }
        public int VendorId { get; init; }
        public int ProductId { get; init; }
        public DateTime PurchaseDate { get; init; }
        public string GrnNumber { get; init; } = string.Empty;
        public decimal GrandTotalAmount { get; init; }
        public decimal Quantity { get; init; }
        public decimal RatePerUnit { get; init; }
    }

    private sealed class AllocationCommissionRow
    {
        public int VendorId { get; init; }
        public DateTime InvoiceDate { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public decimal CommissionAmount { get; init; }
    }

    private sealed class LedgerData
    {
        public List<InvoiceRow> Invoices { get; init; } = new();
        public List<PurchaseRow> Purchases { get; init; } = new();
        public List<AllocationCommissionRow> AllocationCommissions { get; init; } = new();
        public List<LedgerEntry> Entries { get; init; } = new();

        // Older invoices' single commission, split across vendors by FIFO once per sale and shared
        // by every party's ledger.
        public Dictionary<int, List<(int VendorId, decimal Cost)>> LegacyCommissionSplits { get; init; } = new();
    }

    private async Task<LedgerData> LoadLedgerDataAsync()
    {
        var invoices = await _db.SalesInvoices.AsNoTracking()
            .OrderBy(i => i.Id)
            .Select(i => new InvoiceRow
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                BuyerId = i.BuyerId,
                AgentId = i.AgentId,
                ProductId = i.ProductId,
                StockType = i.StockType,
                QuantitySold = i.QuantitySold,
                SubtotalAmount = i.SubtotalAmount,
                GrandTotalAmount = i.GrandTotalAmount,
                AgentCommissionAmount = i.AgentCommissionAmount,
                AgentCommissionPercentage = i.AgentCommissionPercentage,
                HasAllocations = i.Allocations.Any(),
                StockOwnerId = i.ConsignmentReceipt != null ? i.ConsignmentReceipt.StockOwnerId : (int?)null,
                AgreedCommissionPercentage = i.ConsignmentReceipt != null ? i.ConsignmentReceipt.AgreedCommissionPercentage : (decimal?)null
            })
            .ToListAsync();

        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new PurchaseRow
            {
                Id = p.Id,
                VendorId = p.VendorId,
                ProductId = p.ProductId,
                PurchaseDate = p.PurchaseDate,
                GrnNumber = p.GrnNumber,
                GrandTotalAmount = p.GrandTotalAmount,
                Quantity = p.Quantity,
                RatePerUnit = p.RatePerUnit
            })
            .ToListAsync();

        var allocationCommissions = await _db.SalesInvoiceAllocations.AsNoTracking()
            .Where(a => a.CommissionAmount > 0)
            .OrderBy(a => a.Id)
            .Select(a => new AllocationCommissionRow
            {
                VendorId = a.OwnedPurchase!.VendorId,
                InvoiceDate = a.SalesInvoice!.InvoiceDate,
                InvoiceNumber = a.SalesInvoice!.InvoiceNumber,
                CommissionAmount = a.CommissionAmount
            })
            .ToListAsync();

        var entries = await _db.LedgerEntries.AsNoTracking()
            .Where(v => v.Type != LedgerEntryType.ContraAdjustment || v.ApprovedByAdmin)
            .OrderBy(v => v.Id)
            .ToListAsync();

        // Deleted vendors' purchases have always been left out of the FIFO split.
        var deletedPartyIds = (await _db.Parties.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync()).ToHashSet();

        var splits = invoices
            .Where(IsLegacyCommissionedSale)
            .ToDictionary(sale => sale.Id, sale => FifoVendorSplit(sale, invoices, purchases, deletedPartyIds));

        return new LedgerData
        {
            Invoices = invoices,
            Purchases = purchases,
            AllocationCommissions = allocationCommissions,
            Entries = entries,
            LegacyCommissionSplits = splits
        };
    }

    // Invoices issued before lot selection existed carry one invoice-level commission with no
    // recorded source, so those are still resolved via FIFO against purchase history.
    private static bool IsLegacyCommissionedSale(InvoiceRow sale) =>
        sale.StockType == StockType.OwnedStock && sale.AgentCommissionAmount > 0 && !sale.HasAllocations;

    // Skips whatever earlier sales of the same product already consumed, then takes this sale's
    // quantity from the oldest purchases first.
    private static List<(int VendorId, decimal Cost)> FifoVendorSplit(
        InvoiceRow sale, List<InvoiceRow> invoices, List<PurchaseRow> purchases, HashSet<int> deletedPartyIds)
    {
        var lots = purchases
            .Where(p => p.ProductId == sale.ProductId && !deletedPartyIds.Contains(p.VendorId))
            .OrderBy(p => p.PurchaseDate).ThenBy(p => p.Id);

        var toSkip = invoices
            .Where(s => s.ProductId == sale.ProductId
                && s.StockType == StockType.OwnedStock
                && s.Id != sale.Id
                && (s.InvoiceDate < sale.InvoiceDate || (s.InvoiceDate == sale.InvoiceDate && s.Id < sale.Id)))
            .Sum(s => s.QuantitySold);
        var toAllocate = sale.QuantitySold;
        var result = new List<(int VendorId, decimal Cost)>();

        foreach (var lot in lots)
        {
            var available = lot.Quantity;
            if (toSkip > 0)
            {
                var skip = Math.Min(toSkip, available);
                available -= skip;
                toSkip -= skip;
            }

            if (available <= 0 || toAllocate <= 0)
            {
                continue;
            }

            var take = Math.Min(available, toAllocate);
            result.Add((lot.VendorId, Math.Round(take * lot.RatePerUnit, 2)));
            toAllocate -= take;
            if (toAllocate <= 0)
            {
                break;
            }
        }

        return result;
    }

    private static List<PartyLedgerLine> BuildPartyLines(LedgerData data, int partyId)
    {
        var lines = new List<PartyLedgerLine>();

        foreach (var invoice in data.Invoices.Where(i => i.BuyerId == partyId))
        {
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Sales invoice (receivable)",
                Head = HeadType.DirectProductHead,
                Side = DuesSide.Receivable,
                Debit = invoice.GrandTotalAmount
            });
        }

        foreach (var invoice in data.Invoices.Where(i => i.StockType == StockType.ConsignmentStock && i.StockOwnerId == partyId))
        {
            var commissionRevenue = Math.Round(invoice.SubtotalAmount * (invoice.AgreedCommissionPercentage ?? 0) / 100m, 2);

            // Only the net payable belongs on the stock owner's own ledger — the commission
            // itself is company revenue (not money owed to/from this party), so it is tracked
            // company-wide via GetConsignmentCommissionRevenueAsync instead, not on this line.
            lines.Add(new PartyLedgerLine
            {
                Date = invoice.InvoiceDate,
                Document = invoice.InvoiceNumber,
                Description = "Consignment net payable to stock owner",
                Head = HeadType.DirectProductHead,
                Side = DuesSide.Payable,
                Credit = invoice.SubtotalAmount - commissionRevenue
            });
        }

        // Agent commission: accrues on this party's ledger (Commission head) whenever they're
        // set as the earning agent on an invoice — separate from the invoice's own buyer/dues.
        foreach (var invoice in data.Invoices.Where(i => i.AgentId == partyId && i.AgentCommissionAmount > 0))
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

        var partyPurchases = data.Purchases.Where(p => p.VendorId == partyId).ToList();
        foreach (var purchase in partyPurchases)
        {
            lines.Add(new PartyLedgerLine
            {
                Date = purchase.PurchaseDate,
                Document = purchase.GrnNumber,
                Description = "Owned stock purchase (payable)",
                Head = HeadType.DirectProductHead,
                Side = DuesSide.Payable,
                Credit = purchase.GrandTotalAmount
            });
        }

        // Owned-stock commission is cash the vendor owes back separately — it is NOT netted off
        // their payable (the vendor is still paid the full purchase price; see
        // GetPurchaseOutstandingAsync). It only accrues here (Credit) under the Commission head,
        // so it shows as receivable until a Commission-head "Payment Received" ledger entry
        // against this vendor records it as actually collected. Sales that picked their source
        // lots explicitly already carry a per-lot commission, so this party's share is read
        // straight off the rows drawn from their own purchases — no FIFO guesswork needed.
        foreach (var commission in data.AllocationCommissions.Where(a => a.VendorId == partyId))
        {
            lines.Add(new PartyLedgerLine
            {
                Date = commission.InvoiceDate,
                Document = commission.InvoiceNumber,
                Description = $"Commission accrued on invoice {commission.InvoiceNumber}",
                Head = HeadType.CommissionHead,
                Credit = commission.CommissionAmount
            });
        }

        if (partyPurchases.Count > 0)
        {
            var suppliedProducts = partyPurchases.Select(p => p.ProductId).ToHashSet();
            foreach (var sale in data.Invoices.Where(s => IsLegacyCommissionedSale(s) && suppliedProducts.Contains(s.ProductId)))
            {
                var vendorShare = data.LegacyCommissionSplits[sale.Id].FirstOrDefault(a => a.VendorId == partyId);
                if (vendorShare.VendorId != partyId || vendorShare.Cost <= 0)
                {
                    continue;
                }

                var commissionShare = Math.Round(vendorShare.Cost * sale.AgentCommissionPercentage / 100m, 2);
                if (commissionShare > 0)
                {
                    lines.Add(new PartyLedgerLine
                    {
                        Date = sale.InvoiceDate,
                        Document = sale.InvoiceNumber,
                        Description = $"Commission accrued on invoice {sale.InvoiceNumber}",
                        Head = HeadType.CommissionHead,
                        Credit = commissionShare
                    });
                }
            }
        }

        foreach (var entry in data.Entries.Where(e => e.PartyId == partyId))
        {
            var isDues = entry.Head == HeadType.DirectProductHead;
            PartyLedgerLine Line(string description, DuesSide side) => new()
            {
                Date = entry.LedgerDate,
                Document = entry.LedgerNumber,
                Description = description,
                Head = entry.Head,
                Side = isDues ? side : DuesSide.None,
                SortPriority = 1
            };
            var viaMode = $"{entry.Type.GetDisplayName()} via {entry.Mode.GetDisplayName()}";

            switch (entry.Type)
            {
                case LedgerEntryType.PaymentReceived when !isDues:
                    // Commission is accrued as a Credit, so money settling it (received from the
                    // party) is a Debit — same as a payout — rather than adding to what's accrued.
                    var commissionReceived = Line(viaMode, DuesSide.None);
                    commissionReceived.Debit = entry.Amount;
                    lines.Add(commissionReceived);
                    break;
                case LedgerEntryType.PaymentReceived:
                    // Money in settles what this party owes us; it never changes what we owe them.
                    var received = Line(viaMode, DuesSide.Receivable);
                    received.Credit = entry.Amount;
                    lines.Add(received);
                    break;
                case LedgerEntryType.PaymentPaid:
                    var paid = Line(viaMode, DuesSide.Payable);
                    paid.Debit = entry.Amount;
                    lines.Add(paid);
                    break;
                case LedgerEntryType.ContraAdjustment when isDues:
                    // Nets the two sides against each other, so both shrink by the same amount.
                    var againstReceivable = Line("Contra netting (reduces receivable)", DuesSide.Receivable);
                    againstReceivable.Credit = entry.Amount;
                    lines.Add(againstReceivable);
                    var againstPayable = Line("Contra netting (reduces payable)", DuesSide.Payable);
                    againstPayable.Debit = entry.Amount;
                    lines.Add(againstPayable);
                    break;
                case LedgerEntryType.ContraAdjustment:
                    var legacyContra = Line("Contra netting (mutual settlement)", DuesSide.None);
                    legacyContra.Debit = entry.Amount;
                    legacyContra.Credit = entry.Amount;
                    lines.Add(legacyContra);
                    break;
            }
        }

        return lines;
    }
}
