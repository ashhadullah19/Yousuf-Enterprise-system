using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IInvoicePdfService
{
    byte[] Build(SystemSetting settings, SalesInvoice invoice);
}

public class InvoicePdfService : IInvoicePdfService
{
    public InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Build(SystemSetting settings, SalesInvoice invoice)
    {
        var gstLabel = invoice.ApplyGst ? "GST Invoice" : "Non-GST Invoice";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Column(col =>
                {
                    col.Item().Text(settings.CompanyName).FontSize(20).Bold();
                    col.Item().Text(settings.Address);
                    col.Item().Text(settings.ContactNumbers);
                    col.Item().PaddingTop(8).Text($"{gstLabel}  ·  {invoice.InvoiceNumber}").SemiBold();
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Item().Text($"Date: {invoice.InvoiceDate:dd-MMM-yyyy}");
                    col.Item().Text($"Buyer: {invoice.Buyer?.FullName}");
                    col.Item().Text($"Stock: {invoice.StockType}");
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Product");
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Qty");
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Rate");
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Amount");
                        });
                        table.Cell().Padding(4).Text(invoice.Product?.Name);
                        table.Cell().Padding(4).Text($"{invoice.QuantitySold:N4} {invoice.Unit}");
                        table.Cell().Padding(4).Text($"{invoice.RatePerUnit:N2}");
                        table.Cell().Padding(4).Text($"{invoice.SubtotalAmount:N2}");
                    });

                    col.Item().AlignRight().PaddingTop(8).Text($"Subtotal: {invoice.SubtotalAmount:N2}");
                    col.Item().AlignRight().Text($"GST ({invoice.GstPercentage:N2}%): {invoice.GstAmount:N2}");
                    col.Item().AlignRight().Text($"Grand Total: {invoice.GrandTotalAmount:N2}").Bold();

                    col.Item().PaddingTop(16).Text($"Payment Type: {invoice.PaymentType}");
                    if (invoice.PaymentType == PaymentType.Credit && invoice.DueDate.HasValue)
                    {
                        col.Item().Text($"Payment Due: {invoice.DueDate.Value:dd-MMM-yyyy}").SemiBold();
                    }
                    // Commission, cost, and profit are internal figures and are
                    // intentionally not printed on the customer-facing invoice.
                });
            });
        }).GeneratePdf();
    }
}