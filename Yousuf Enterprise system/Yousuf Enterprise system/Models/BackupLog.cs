using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public class BackupLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string FileSizeBytes { get; set; } = "0";

    [StringLength(50)]
    public string UploadStatus { get; set; } = "Pending";

    [StringLength(200)]
    public string? DriveFileId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
