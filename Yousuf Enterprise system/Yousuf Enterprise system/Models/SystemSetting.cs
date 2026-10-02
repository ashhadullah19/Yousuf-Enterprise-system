using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class SystemSetting
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string CompanyName { get; set; } = "Yousuf Enterprise";

    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [StringLength(200)]
    public string ContactNumbers { get; set; } = string.Empty;

    [StringLength(500)]
    public string? LogoPath { get; set; }

    [Range(0, 100)]
    public decimal DefaultGstPercentage { get; set; } = 18.00m;

    [Display(Name = "Bag Weight (KG)"), Range(0.0001, double.MaxValue, ErrorMessage = "Bag weight must be greater than 0.")]
    public decimal BagWeightKg { get; set; } = 50m;

    // Where backups should ultimately end up — shown on the Backups page as a reminder/reference.
    // Automatic upload isn't wired up yet (that needs real Google API credentials, not just a
    // folder link), so backups are downloaded here and uploaded to this folder manually for now.
    [Display(Name = "Google Drive Backup Folder Link"), StringLength(500), DataType(DataType.Url)]
    public string? GoogleDriveFolderLink { get; set; }
}
