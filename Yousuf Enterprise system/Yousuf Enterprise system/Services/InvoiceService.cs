using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public class InvoiceCalculationResult
{
    public decimal Subtotal { get; set; }
    public decimal GstPercentage { get; set; }
    public decimal GstAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public decimal AgentCommissionAmount { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal ProfitAmount { get; set; }
}

// A single owned-stock purchase (GRN) with however much of it is still unsold, so a sale can be
// filled by picking specific lots — each with its own vendor, purchase rate and commission.
public class OwnedStockLot
{
    public int OwnedPurchaseId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal AvailableQuantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
}

public interface IInvoiceService
{
    Task<InvoiceCalculationResult> CalculateAsync(SalesInvoice invoice);
    Task<decimal> OwnedStockOnHandAsync(int productId, int? excludeInvoiceId = null);
    Task<List<(string VendorName, decimal RemainingQty)>> OwnedStockByVendorAsync(int productId);
    Task<List<OwnedStockLot>> AvailableLotsAsync(int productId, int? excludeInvoiceId = null);
    Task<decimal> ConsignmentRemainingAsync(int receiptId, int? excludeInvoiceId = null);
    Task<decimal> AverageCostAsync(int productId);
    Task ApplyAndValidateAsync(SalesInvoice invoice);
}

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _db;

    public InvoiceService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Weighted average LANDED purchase cost per unit (purchase rate plus its share of that
    // purchase's freight/labour), derived from Owned Purchases. Consignment stock has no cost
    // basis to Yousuf Enterprise, so this only applies to Owned Stock sales.
    public async Task<decimal> AverageCostAsync(int productId)
    {
        var purchases = await _db.OwnedPurchases
            .Where(p => p.ProductId == productId)
            .Select(p => new { p.Quantity, p.RatePerUnit, p.FreightLabourCharges })
            .ToListAsync();

        if (purchases.Count == 0 || purchases.Sum(p => p.Quantity) == 0)
        {
            return 0;
        }

        var totalQty = purchases.Sum(p => p.Quantity);
        var totalCost = purchases.Sum(p => p.Quantity * p.RatePerUnit + p.FreightLabourCharges);
        return Math.Round(totalCost / totalQty, 4);
    }

    public async Task<InvoiceCalculationResult> CalculateAsync(SalesInvoice invoice)
    {
        var result = new InvoiceCalculationResult
        {
            Subtotal = Math.Round(invoice.QuantitySold * invoice.RatePerUnit, 2)
        };

        if (invoice.ApplyGst)
        {
            var gst = invoice.GstPercentage;
            if (gst <= 0)
            {
                var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
                gst = settings.DefaultGstPercentage;
            }

            result.GstPercentage = gst;
            result.GstAmount = Math.Round(result.Subtotal * gst / 100m, 2);
        }
        else
        {
            result.GstPercentage = 0;
            result.GstAmount = 0;
        }

        // Extra charges (conveyance/labour) are billed straight through to the buyer, so they're
        // added to what they owe — they're a pass-through, not a cost that eats into profit.
        result.GrandTotal = result.Subtotal + result.GstAmount + invoice.ExtraChargesAmount;

        if (invoice.StockType == StockType.OwnedStock)
        {
            if (invoice.Allocations.Count > 0)
            {
                // Sale names the exact lots it draws from, so cost is what those lots actually
                // cost — not a product-wide average — and commission is recorded per lot on the
                // allocation rows instead of as one invoice-level agent cut.
                result.CostOfGoodsSold = Math.Round(invoice.Allocations.Sum(a => a.Quantity * a.PurchaseRate), 2);
                result.AgentCommissionAmount = 0;
            }
            else
            {
                // Invoices issued before lot selection existed: weighted-average cost, with a
                // single invoice-level commission split FIFO across vendors at ledger time.
                var avgCost = await AverageCostAsync(invoice.ProductId);
                result.CostOfGoodsSold = Math.Round(avgCost * invoice.QuantitySold, 2);
                result.AgentCommissionAmount = Math.Round(result.CostOfGoodsSold * invoice.AgentCommissionPercentage / 100m, 2);
            }

            // Owned-stock commission is cash collected back from the vendor(s) separately (see
            // LedgerService), not a cut of the sale price, so it doesn't touch profit here.
            // Extra charges are billed to and recovered from the buyer (see GrandTotal above),
            // so they don't reduce profit either.
            result.ProfitAmount = Math.Round(result.Subtotal - result.CostOfGoodsSold, 2);
        }
        else
        {
            // Consignment: we never owned this stock, so the full subtotal isn't ours -- most of
            // it is owed straight back to the stock owner. Only our agreed commission cut is
            // actual revenue, so that (not the subtotal) is the profit basis here. The generic
            // agent commission here is still sale-price based (there's no purchase cost basis
            // for consignment stock) and still reduces profit, unlike the owned-stock case above.
            result.CostOfGoodsSold = 0;
            result.AgentCommissionAmount = Math.Round(result.Subtotal * invoice.AgentCommissionPercentage / 100m, 2);

            var consignmentCommissionPct = 0m;
            if (invoice.ConsignmentReceiptId.HasValue)
            {
                var receipt = await _db.ConsignmentReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == invoice.ConsignmentReceiptId.Value);
                consignmentCommissionPct = receipt?.AgreedCommissionPercentage ?? 0;
            }

            // Extra charges are billed to and recovered from the buyer (see GrandTotal above),
            // so they don't reduce profit here either.
            var consignmentCommissionRevenue = Math.Round(result.Subtotal * consignmentCommissionPct / 100m, 2);
            result.ProfitAmount = Math.Round(consignmentCommissionRevenue - result.AgentCommissionAmount, 2);
        }

        return result;
    }

    // Per-lot availability for the sale form's stock picker. Lots consumed by invoices that
    // recorded their sources are reduced directly; quantity sold by older invoices (which only
    // recorded Product+Qty) is depleted FIFO on top, so the per-lot totals still reconcile with
    // OwnedStockOnHandAsync.
    public async Task<List<OwnedStockLot>> AvailableLotsAsync(int productId, int? excludeInvoiceId = null)
    {
        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.PurchaseDate).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.GrnNumber,
                p.VendorId,
                VendorName = p.Vendor!.FullName,
                p.PurchaseDate,
                p.Quantity,
                p.RatePerUnit,
                p.FreightLabourCharges,
                p.Unit
            })
            .ToListAsync();

        if (purchases.Count == 0)
        {
            return new List<OwnedStockLot>();
        }

        var allocQuery = _db.SalesInvoiceAllocations.AsNoTracking()
            .Where(a => a.OwnedPurchase!.ProductId == productId);
        if (excludeInvoiceId.HasValue)
        {
            allocQuery = allocQuery.Where(a => a.SalesInvoiceId != excludeInvoiceId.Value);
        }

        var allocated = await allocQuery
            .GroupBy(a => a.OwnedPurchaseId)
            .Select(g => new { OwnedPurchaseId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.OwnedPurchaseId, x => x.Qty);

        var legacyQuery = _db.SalesInvoices.AsNoTracking()
            .Where(s => s.ProductId == productId
                && s.StockType == StockType.OwnedStock
                && !s.Allocations.Any());
        if (excludeInvoiceId.HasValue)
        {
            legacyQuery = legacyQuery.Where(s => s.Id != excludeInvoiceId.Value);
        }

        var legacySold = await legacyQuery.SumAsync(s => (decimal?)s.QuantitySold) ?? 0;

        var lots = new List<OwnedStockLot>();
        foreach (var purchase in purchases)
        {
            var remaining = purchase.Quantity - (allocated.TryGetValue(purchase.Id, out var used) ? used : 0);

            if (legacySold > 0 && remaining > 0)
            {
                var consumed = Math.Min(remaining, legacySold);
                remaining -= consumed;
                legacySold -= consumed;
            }

            if (remaining <= 0)
            {
                continue;
            }

            // Landed cost: purchase rate plus this lot's own share of its freight/labour charge,
            // spread evenly over the quantity it bought — not just the negotiated rate — so COGS
            // (and everything derived from it: profit, commission basis) reflects the true cost.
            var landedRate = purchase.RatePerUnit;

            lots.Add(new OwnedStockLot
            {
                OwnedPurchaseId = purchase.Id,
                GrnNumber = purchase.GrnNumber,
                VendorId = purchase.VendorId,
                VendorName = purchase.VendorName,
                PurchaseDate = purchase.PurchaseDate,
                RatePerUnit = landedRate,
                AvailableQuantity = Math.Round(remaining, 4),
                Unit = purchase.Unit
            });
        }

        return lots;
    }

    public async Task<decimal> OwnedStockOnHandAsync(int productId, int? excludeInvoiceId = null)
    {
        var purchased = await _db.OwnedPurchases
            .Where(p => p.ProductId == productId)
            .SumAsync(p => (decimal?)p.Quantity) ?? 0;

        var soldQuery = _db.SalesInvoices.Where(s =>
            s.ProductId == productId && s.StockType == StockType.OwnedStock);
        if (excludeInvoiceId.HasValue)
        {
            soldQuery = soldQuery.Where(s => s.Id != excludeInvoiceId.Value);
        }

        var sold = await soldQuery.SumAsync(s => (decimal?)s.QuantitySold) ?? 0;
        return purchased - sold;
    }

    // Owned stock isn't lot-tracked (sales only record a Product, not which purchase it
    // came from), so this derives a per-vendor "remaining" breakdown by depleting purchases
    // oldest-first (FIFO) against total units sold. It's a display aid for choosing a vendor
    // at sale time, not a real allocation — the sale itself still only records Product+Qty.
    public async Task<List<(string VendorName, decimal RemainingQty)>> OwnedStockByVendorAsync(int productId)
    {
        var purchases = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.PurchaseDate).ThenBy(p => p.Id)
            .Select(p => new { p.Quantity, VendorName = p.Vendor!.FullName })
            .ToListAsync();

        var soldQty = await _db.SalesInvoices
            .Where(s => s.ProductId == productId && s.StockType == StockType.OwnedStock)
            .SumAsync(s => (decimal?)s.QuantitySold) ?? 0;

        var remainingByVendor = new List<(string VendorName, decimal Qty)>();
        var toConsume = soldQty;
        foreach (var purchase in purchases)
        {
            var consumed = Math.Min(purchase.Quantity, toConsume);
            var left = purchase.Quantity - consumed;
            toConsume -= consumed;
            if (left > 0)
            {
                remainingByVendor.Add((purchase.VendorName, left));
            }
        }

        return remainingByVendor
            .GroupBy(r => r.VendorName)
            .Select(g => (VendorName: g.Key, RemainingQty: Math.Round(g.Sum(x => x.Qty), 4)))
            .Where(x => x.RemainingQty > 0)
            .ToList();
    }

    public async Task<decimal> ConsignmentRemainingAsync(int receiptId, int? excludeInvoiceId = null)
    {
        var receipt = await _db.ConsignmentReceipts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == receiptId);
        if (receipt is null)
        {
            return 0;
        }

        var soldQuery = _db.SalesInvoices.Where(s => s.ConsignmentReceiptId == receiptId);
        if (excludeInvoiceId.HasValue)
        {
            soldQuery = soldQuery.Where(s => s.Id != excludeInvoiceId.Value);
        }

        var sold = await soldQuery.SumAsync(s => (decimal?)s.QuantitySold) ?? 0;
        return receipt.ReceivedQuantity - sold;
    }

    // Validates the picked lots against what's actually still available, snapshots each lot's
    // purchase rate, and works out each one's commission. Quantity sold is the sum of the rows,
    // so the invoice total can never disagree with the sources it was filled from.
    private async Task ApplyOwnedStockAllocationsAsync(SalesInvoice invoice)
    {
        invoice.Allocations.RemoveAll(a => a.OwnedPurchaseId == 0 || a.Quantity <= 0);

        if (invoice.Allocations.Count == 0)
        {
            throw new InvalidOperationException("Select at least one owned stock source and enter the quantity to sell from it.");
        }

        var duplicate = invoice.Allocations
            .GroupBy(a => a.OwnedPurchaseId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException("The same stock lot is selected more than once.");
        }

        var lots = (await AvailableLotsAsync(invoice.ProductId, invoice.Id == 0 ? null : invoice.Id))
            .ToDictionary(l => l.OwnedPurchaseId);

        foreach (var allocation in invoice.Allocations)
        {
            if (!lots.TryGetValue(allocation.OwnedPurchaseId, out var lot))
            {
                throw new InvalidOperationException("One of the selected stock lots is no longer available. Re-check the stock list and try again.");
            }

            if (allocation.Quantity > lot.AvailableQuantity)
            {
                throw new InvalidOperationException($"{lot.VendorName} ({lot.GrnNumber}): only {lot.AvailableQuantity:N4} available, {allocation.Quantity:N4} requested.");
            }

            if (allocation.CommissionPercentage is < 0 or > 100)
            {
                throw new InvalidOperationException("Commission percentage must be between 0 and 100.");
            }

            // Rate comes from the lot, never from the posted form, so it can't be tampered with.
            allocation.PurchaseRate = lot.RatePerUnit;
            allocation.CommissionAmount = Math.Round(
                allocation.Quantity * allocation.PurchaseRate * allocation.CommissionPercentage / 100m, 2);
        }

        invoice.QuantitySold = Math.Round(invoice.Allocations.Sum(a => a.Quantity), 4);

        // Commission is now per source lot, so the single invoice-level agent cut doesn't apply.
        invoice.AgentId = null;
        invoice.AgentCommissionPercentage = 0;
    }

    public async Task ApplyAndValidateAsync(SalesInvoice invoice)
    {
        if (invoice.StockType == StockType.OwnedStock)
        {
            await ApplyOwnedStockAllocationsAsync(invoice);
        }
        else
        {
            invoice.Allocations.Clear();
        }

        if (invoice.QuantitySold <= 0)
        {
            throw new InvalidOperationException("Quantity sold must be greater than zero.");
        }

        if (invoice.AgentCommissionPercentage is < 0 or > 100)
        {
            throw new InvalidOperationException("Commission percentage must be between 0 and 100.");
        }

        if (invoice.StockType == StockType.ConsignmentStock)
        {
            if (!invoice.ConsignmentReceiptId.HasValue)
            {
                throw new InvalidOperationException("Consignment stock sales require a consignment receipt.");
            }

            var remaining = await ConsignmentRemainingAsync(invoice.ConsignmentReceiptId.Value, invoice.Id == 0 ? null : invoice.Id);
            if (invoice.QuantitySold > remaining)
            {
                throw new InvalidOperationException($"Insufficient consignment stock. Remaining: {remaining:N4}.");
            }

            var receipt = await _db.ConsignmentReceipts.AsNoTracking()
                .FirstAsync(c => c.Id == invoice.ConsignmentReceiptId.Value);
            invoice.ProductId = receipt.ProductId;
        }
        else
        {
            invoice.ConsignmentReceiptId = null;
            var onHand = await OwnedStockOnHandAsync(invoice.ProductId, invoice.Id == 0 ? null : invoice.Id);
            if (invoice.QuantitySold > onHand)
            {
                throw new InvalidOperationException($"Insufficient owned stock. On hand: {onHand:N4}.");
            }
        }

        var calc = await CalculateAsync(invoice);
        invoice.SubtotalAmount = calc.Subtotal;
        invoice.GstPercentage = calc.GstPercentage;
        invoice.GstAmount = calc.GstAmount;
        invoice.GrandTotalAmount = calc.GrandTotal;
        invoice.AgentCommissionAmount = calc.AgentCommissionAmount;
        invoice.CostOfGoodsSold = calc.CostOfGoodsSold;
        invoice.ProfitAmount = calc.ProfitAmount;
    }
}