using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.ViewModels;

public class StockAllocationPickerViewModel
{
    public List<OwnedStockLot> Lots { get; set; } = new();

    // Rows the user had already filled in — used to restore their input when the form comes
    // back after a validation failure.
    public List<SalesInvoiceAllocation> Selected { get; set; } = new();
}
