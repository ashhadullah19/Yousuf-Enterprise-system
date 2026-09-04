using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class Party
{
    public int Id { get; set; }

    [Display(Name = "Party Type")]
    public PartyType Type { get; set; }

    [Required, StringLength(200), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "Father Name")]
    public string? FatherName { get; set; }

    [StringLength(13), Display(Name = "CNIC"), RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be 13 digits.")]
    public string? Cnic { get; set; }

    [StringLength(30), Display(Name = "Primary Contact")]
    public string? PrimaryContact { get; set; }

    [StringLength(30), Display(Name = "Secondary Contact")]
    public string? SecondaryContact { get; set; }

    [StringLength(500), Display(Name = "Business Address")]
    public string? BusinessAddress { get; set; }

    [StringLength(50), Display(Name = "NTN")]
    public string? Ntn { get; set; }

    [StringLength(50), Display(Name = "STRN / SBR")]
    public string? StrnSbr { get; set; }

    [Display(Name = "Filer Status")]
    public FilerStatus FilerStatus { get; set; } = FilerStatus.NonFiler;

    [StringLength(100), Display(Name = "Bank Name")]
    public string? BankName { get; set; }

    [StringLength(200), Display(Name = "Account Title")]
    public string? AccountTitle { get; set; }

    [StringLength(50), Display(Name = "Account Number")]
    public string? AccountNumber { get; set; }

    [StringLength(34), Display(Name = "IBAN")]
    public string? Iban { get; set; }

    [StringLength(20), Display(Name = "Branch Code")]
    public string? BranchCode { get; set; }

    public string? CnicFrontPath { get; set; }
    public string? CnicBackPath { get; set; }
    public string? CanceledChequePath { get; set; }
    public string? TaxCertificatePath { get; set; }

    public bool IsDeleted { get; set; }
}
