using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public enum PartyType
{
    Vendor = 1,
    Buyer = 2,
    Both = 3
}

public enum FilerStatus
{
    [Display(Name = "Active Filer")]
    ActiveFiler = 1,
    [Display(Name = "Non-Filer")]
    NonFiler = 2
}

public enum UnitOfMeasure
{
    KG = 1,
    Ton = 2,
    Bag = 3,
    Maund = 4
}

public enum StockType
{
    [Display(Name = "Owned Stock")]
    OwnedStock = 1,
    [Display(Name = "Consignment Stock")]
    ConsignmentStock = 2
}

public enum LedgerEntryType
{
    [Display(Name = "Payment Received")]
    PaymentReceived = 1,
    [Display(Name = "Payment Paid")]
    PaymentPaid = 2,
    [Display(Name = "Contra Adjustment")]
    ContraAdjustment = 3
}

public enum HeadType
{
    [Display(Name = "Direct Product")]
    DirectProductHead = 1,
    [Display(Name = "Commission")]
    CommissionHead = 2
}

public enum PaymentMode
{
    Cash = 1,
    Cheque = 2,
    [Display(Name = "Online Bank Transfer")]
    OnlineBankTransfer = 3
}
public enum PaymentType
{
    Cash,
    Online,
    Credit // pay later — requires DueDate
}