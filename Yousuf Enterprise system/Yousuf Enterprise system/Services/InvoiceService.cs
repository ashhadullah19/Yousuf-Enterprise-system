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
    public decimal CommissionRevenue { get; set; }
    public decimal StockOwnerPayable { get; set; }
}

public interface IInvoiceService
{
    Task<InvoiceCalculationResult> CalculateAsync(SalesInvoice invoice);
    Task<decimal> OwnedStockOnHandAsync(int productId, int? excludeInvoiceId = null);
    Task<decimal> ConsignmentRemainingAsync(int receiptId, int? excludeInvoiceId = null);
    Task ApplyAndValidateAsync(SalesInvoice invoice);
}

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _db;

    public InvoiceService(ApplicationDbContext db)
    {
        _db = db;
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
            result.GrandTotal = result.Subtotal + result.GstAmount;
        }
        else
        {
            result.GstPercentage = 0;
            result.GstAmount = 0;
            result.GrandTotal = result.Subtotal;
        }

        if (invoice.StockType == StockType.ConsignmentStock && invoice.ConsignmentReceiptId.HasValue)
        {
            var receipt = await _db.ConsignmentReceipts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == invoice.ConsignmentReceiptId.Value);
            if (receipt is not null)
            {
                result.CommissionRevenue = Math.Round(result.Subtotal * receipt.AgreedCommissionPercentage / 100m, 2);
                result.StockOwnerPayable = result.Subtotal - result.CommissionRevenue;
            }
        }

        return result;
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

    public async Task ApplyAndValidateAsync(SalesInvoice invoice)
    {
        if (invoice.QuantitySold <= 0)
        {
            throw new InvalidOperationException("Quantity sold must be greater than zero.");
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
        invoice.CommissionRevenue = calc.CommissionRevenue;
        invoice.StockOwnerPayable = calc.StockOwnerPayable;
    }
}
