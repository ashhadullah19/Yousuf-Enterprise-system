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
}
