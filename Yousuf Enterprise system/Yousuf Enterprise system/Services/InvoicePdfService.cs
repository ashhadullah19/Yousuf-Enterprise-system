using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IInvoicePdfService
{
    byte[] Build(SystemSetting settings, SalesInvoice invoice);
}

// Matches the company's existing paper invoice format (Sr/Item/Quantity/Net Wt/Rate/Total table,
// boxed Amount Due, Terms & Conditions / Signature / Thank you footer) so the printed PDF looks
// like what staff already hand-write, instead of a generic invoice layout.
public class InvoicePdfService : IInvoicePdfService
{
    public InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Build(SystemSetting settings, SalesInvoice invoice)
    {
        // "Quantity" is the figure recorded in whatever unit the sale was negotiated in (e.g. bag
        // count); "Net Wt" is that same quantity converted to a weight unit, when one was set up
        // for this sale — the same distinction the paper invoice draws between its two columns.
        var quantityText = $"{invoice.QuantitySold:N4} {invoice.Unit}";
        var netWeightText = invoice.DisplayUnit.HasValue
            ? $"{UnitConversion.Convert(invoice.QuantitySold, invoice.Unit, invoice.DisplayUnit.Value, settings.BagWeightKg):N4} {invoice.DisplayUnit}"
            : quantityText;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(settings.CompanyName.ToUpperInvariant()).FontSize(26).Bold();
                    if (!string.IsNullOrWhiteSpace(settings.Address))
                    {
                        col.Item().AlignCenter().PaddingTop(2).Text(settings.Address.ToUpperInvariant()).FontSize(16).Bold();
                    }
                    //if (!string.IsNullOrWhiteSpace(settings.ContactNumbers))
                    //{
                    //    col.Item().AlignCenter().Text(settings.ContactNumbers);
                    //}
                    col.Item().AlignCenter().PaddingTop(6).Text($"Bill to: {invoice.Buyer?.FullName}").FontSize(14).SemiBold();

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text("INVOICE").FontSize(15).Bold().Underline();
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            if (invoice.PaymentType == PaymentType.Credit && invoice.DueDate.HasValue)
                            {
                                c.Item().Text($"Due: {invoice.DueDate.Value:dd-MM-yyyy}").FontSize(9).FontColor(Colors.Red.Darken1);
                            }
                            c.Item().Text($"Invoice #: {invoice.InvoiceNumber}");
                            c.Item().Text($"Invoice Date: {invoice.InvoiceDate:dd-MM-yyyy}");
                        });
                    });
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            void HeaderCell(string text, bool right = false)
                            {
                                // Border/background go on the full-width cell first; alignment
                                // only affects the text inside it — reversing the order shrinks
                                // the bordered box down to the text's own width instead.
                                var cell = h.Cell().Border(1).BorderColor(Colors.Black)
                                    .Background(Colors.Grey.Lighten3).Padding(5);
                                (right ? cell.AlignRight() : cell).Text(text).Bold();
                            }

                            HeaderCell("Sr\nno");
                            HeaderCell("Item Name");
                            HeaderCell("Quantity");
                            HeaderCell("Net Wt");
                            HeaderCell("Rate", right: true);
                            HeaderCell("Total", right: true);
                        });

                        void Cell(string text, bool right = false)
                        {
                            var cell = table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5);
                            (right ? cell.AlignRight() : cell).Text(text);
                        }

                        Cell("1");
                        Cell(invoice.Product?.Name ?? "");
                        Cell(quantityText);
                        Cell(netWeightText);
                        Cell($"Rs {invoice.RatePerUnit:N2}", right: true);
                        Cell($"Rs {invoice.SubtotalAmount:N0}", right: true);

                        // A couple of blank rows, same as the paper form leaving room for a second line item.
                        for (var i = 0; i < 2; i++)
                        {
                            table.Cell().ColumnSpan(6).Border(1).BorderColor(Colors.Grey.Lighten1).Height(22);
                        }
                    });

                    if (invoice.ExtraChargesAmount > 0)
                    {
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(260).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Text(
                                $"Extra Charges ({invoice.ExtraChargesDescription ?? "misc"}): Rs {invoice.ExtraChargesAmount:N0}").FontSize(9);
                        });
                    }

                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        c.Item().Width(260).Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal:");
                            r.ConstantItem(100).AlignRight().Text($"Rs {invoice.SubtotalAmount:N0}");
                        });
                        if (invoice.ApplyGst)
                        {
                            c.Item().Width(260).Row(r =>
                            {
                                r.RelativeItem().Text($"GST ({invoice.GstPercentage:N2}%):");
                                r.ConstantItem(100).AlignRight().Text($"Rs {invoice.GstAmount:N0}");
                            });
                        }
                    });

                    col.Item().PaddingTop(6).AlignRight().Width(260).Border(1).BorderColor(Colors.Black)
                        .Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Text("Amount Due:").Bold();
                        row.ConstantItem(110).AlignRight().Text($"Rs {invoice.GrandTotalAmount:N0}").Bold();
                    });

                    col.Item().PaddingTop(30).Text("Terms & Conditions:").SemiBold();
                    if (!string.IsNullOrWhiteSpace(invoice.TermsAndConditions))
                    {
                        col.Item().PaddingTop(2).Text(invoice.TermsAndConditions).FontSize(9);
                    }
                    col.Item().PaddingTop(30).Text("Signature:");
                    // Commission, cost, and profit are internal figures and are
                    // intentionally not printed on the customer-facing invoice.
                });

                page.Footer().AlignRight().PaddingTop(10).Text("THANK YOU").FontSize(13).Bold().Underline();
            });
        }).GeneratePdf();
    }
}
