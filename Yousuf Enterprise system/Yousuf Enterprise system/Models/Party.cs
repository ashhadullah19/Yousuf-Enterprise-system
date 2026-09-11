using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class Party : IValidatableObject
{
    public int Id { get; set; }

    [Display(Name = "Party Type")]
    public PartyType Type { get; set; }

    [Required, StringLength(200), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "Father Name")]
    public string? FatherName { get; set; }



    [Required, StringLength(30), Display(Name = "Primary Contact")]
    public string PrimaryContact { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "Secondary Contact")]
    public string? SecondaryContact { get; set; }

    [StringLength(500), Display(Name = "Business Address")]
    public string? BusinessAddress { get; set; }

    [StringLength(50), Display(Name = "NTN")]
    public string? Ntn { get; set; }

    [StringLength(50), Display(Name = "STRN / SBR")]
    public string? StrnSbr { get; set; }

    [Display(Name = "Has Bank Details")]
    public bool HasBankDetails { get; set; }

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


    public bool IsDeleted { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HasBankDetails)
        {
            if (string.IsNullOrWhiteSpace(BankName))
            {
                yield return new ValidationResult("Bank name is required when bank details are on.", new[] { nameof(BankName) });
            }
            if (string.IsNullOrWhiteSpace(AccountTitle))
            {
                yield return new ValidationResult("Account title is required when bank details are on.", new[] { nameof(AccountTitle) });
            }
            if (string.IsNullOrWhiteSpace(AccountNumber))
            {
                yield return new ValidationResult("Account number is required when bank details are on.", new[] { nameof(AccountNumber) });
            }
        }
    }
}
