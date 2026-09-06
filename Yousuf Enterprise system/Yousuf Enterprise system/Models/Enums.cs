namespace Yousuf_Enterprise_system.Models;

public enum PartyType
{
    Vendor = 1,
    Buyer = 2,
    Both = 3
}

public enum FilerStatus
{
    ActiveFiler = 1,
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
    OwnedStock = 1,
    ConsignmentStock = 2
}

public enum VoucherType
{
    PaymentReceived = 1,
    PaymentPaid = 2,
    ContraAdjustment = 3
}

public enum HeadType
{
    DirectProductHead = 1,
    CommissionHead = 2
}

public enum PaymentMode
{
    Cash = 1,
    Cheque = 2,
    OnlineBankTransfer = 3
}
public enum PaymentType
{
    Cash,
    Online,
    Credit // pay later — requires DueDate
}