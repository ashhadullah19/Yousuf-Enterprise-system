# Yousuf Enterprise ERP - Functional & Technical Requirements

## 1. Project Overview
A web-based Enterprise Resource Planning (ERP) system designed for Yousuf Enterprise to digitize operations including dual-role client management, segregated stock tracking (Owned vs. Consignment), multi-head ledgers, GST invoicing, and cloud backups.

---

## 2. Technical Stack Specifications
* **Framework:** ASP.NET Core 8.0 / 9.0 MVC
* **Database:** SQL Server (Entity Framework Core)
* **Authentication:** ASP.NET Core Identity (Role-based Authorization)
* **File Processing:** EPPlus / ClosedXML (Excel Import/Export), QuestPDF / Rotativa (PDF Generation)
* **Background Jobs:** Native IHostedService / Hangfire (Google Drive Backups)

---

## 3. Core Domain Entities & Database Schema

### Module 1: System Settings & Company Profile
```csharp
public class SystemSetting 
{
    public int Id { get; set; }
    public string CompanyName { get; set; } // Default: "Yousuf Enterprise"
    public string Address { get; set; }
    public string ContactNumbers { get; set; }
    public string LogoPath { get; set; }
    public decimal DefaultGstPercentage { get; set; } // e.g., 18.00
}
Module 2: Unified Party Master (Vendors & Buyers)Entities can act as Vendors, Buyers, or Both under a single profile.C#public enum PartyType { Vendor, Buyer, Both }
public enum FilerStatus { ActiveFiler, NonFiler }

public class Party 
{
    public int Id { get; set; }
    public PartyType Type { get; set; }
    public string FullName { get; set; }
    public string FatherName { get; set; }
    public string Cnic { get; set; } // 13 digits
    public string PrimaryContact { get; set; }
    public string SecondaryContact { get; set; }
    public string BusinessAddress { get; set; }
    
    // Tax Details
    public string Ntn { get; set; }
    public string StrnSbr { get; set; }
    public FilerStatus FilerStatus { get; set; }
    
    // Banking Details
    public string BankName { get; set; }
    public string AccountTitle { get; set; }
    public string AccountNumber { get; set; }
    public string Iban { get; set; }
    public string BranchCode { get; set; }
    
    // Attachments (File Paths)
    public string CnicFrontPath { get; set; }
    public string CnicBackPath { get; set; }
    public string CanceledChequePath { get; set; }
    public string TaxCertificatePath { get; set; }
    
    public bool IsDeleted { get; set; } // Soft Delete
}
Module 3: Product MasterC#public enum UnitOfMeasure { KG, Ton, Bag, Maund }

public class Product 
{
    public int Id { get; set; }
    public string Name { get; set; } // e.g., Chia Seed, Gond Katira, Kalonji
    public string Description { get; set; }
    public UnitOfMeasure Uom { get; set; }
    public decimal MinimumStockAlertQty { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}
Module 4: Inventory Management (Segregated Stock)A. Owned Inventory (Direct Purchases)C#public class OwnedPurchase 
{
    public int Id { get; set; }
    public string GrnNumber { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int VendorId { get; set; }
    public Party Vendor { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public decimal Quantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalProductCost { get; set; }
    public decimal FreightLabourCharges { get; set; }
    public decimal GrandTotalAmount { get; set; }
    public string Remarks { get; set; }
}
B. Consignment Inventory (Third-Party Stock)C#public class ConsignmentReceipt 
{
    public int Id { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int StockOwnerId { get; set; }
    public Party StockOwner { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public decimal AgreedCommissionPercentage { get; set; } // e.g., 20.00%
    public string Remarks { get; set; }
    public bool IsDeleted { get; set; }
}
Module 5: Sales & Invoicing EngineC#public enum StockType { OwnedStock, ConsignmentStock }

public class SalesInvoice 
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public int BuyerId { get; set; }
    public Party Buyer { get; set; }
    public StockType StockType { get; set; }
    public int? ConsignmentReceiptId { get; set; } // Required if ConsignmentStock
    public ConsignmentReceipt ConsignmentReceipt { get; set; }
    
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal SubtotalAmount { get; set; }
    
    // Tax Engine
    public bool ApplyGst { get; set; }
    public decimal GstPercentage { get; set; }
    public decimal GstAmount { get; set; }
    public decimal GrandTotalAmount { get; set; }
}
Module 6: Financial Ledgers & VouchersC#public enum VoucherType { PaymentReceived, PaymentPaid, ContraAdjustment }
public enum HeadType { DirectProductHead, CommissionHead }
public enum PaymentMode { Cash, Cheque, OnlineBankTransfer }

public class FinancialVoucher 
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; }
    public DateTime VoucherDate { get; set; }
    public int PartyId { get; set; }
    public Party Party { get; set; }
    public VoucherType Type { get; set; }
    public HeadType Head { get; set; }
    public PaymentMode Mode { get; set; }
    public string ReferenceNumber { get; set; } // Cheque/Transaction ID
    public string BankName { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; }
    public bool ApprovedByAdmin { get; set; } // Required for Contra Adjustment
}
Module 7: Disaster Recovery & Cloud Backup LogsC#public class BackupLog 
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string FileSizeBytes { get; set; }
    public string UploadStatus { get; set; } // Success / Failed
    public string DriveFileId { get; set; }
}
4. Key Business Logic & Automated CalculationsConsignment Sale Auto-Split:When an invoice is issued for Consignment Stock:$$\text{Commission Revenue} = \text{Subtotal} \times \left(\frac{\text{AgreedCommissionPercentage}}{100}\right)$$$$\text{Stock Owner Payable} = \text{Subtotal} - \text{Commission Revenue}$$Automate ledger entries:Credit Commission Income Head with Commission Revenue.Credit Stock Owner Direct Head with Net Payable Amount.GST Invoicing Rules:If ApplyGst == true:$$\text{GST Amount} = \text{SubtotalAmount} \times \left(\frac{\text{GstPercentage}}{100}\right)$$$$\text{Grand Total} = \text{SubtotalAmount} + \text{GST Amount}$$If ApplyGst == false: GST Amount = 0, Grand Total = Subtotal Amount.Contra Netting (Mutual Settlement):Parties acting as Both (Vendor + Buyer) can have mutual payables/receivables offset.Action requires Super Admin approval before posting a ContraAdjustment voucher.5. Security & User Access MatrixRolePermissionsSuper AdminFull System Access, User Creation, Contra Approval, System Settings, Data Backups & Restores.Accountant / OperatorAdd/Edit Master Data, Issue Invoices, Post Vouchers, View Reports. Restricted from deleting records or approving contra netting.