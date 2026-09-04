using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IBackupService
{
    Task<BackupLog> RunBackupAsync(string trigger);
}

public class BackupService : IBackupService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public BackupService(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<BackupLog> RunBackupAsync(string trigger)
    {
        var backupDir = Path.Combine(_env.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(backupDir);
        var fileName = $"yousuf-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        var path = Path.Combine(backupDir, fileName);

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            if (Directory.Exists(uploads))
            {
                foreach (var file in Directory.GetFiles(uploads, "*", SearchOption.AllDirectories))
                {
                    var entryName = Path.GetRelativePath(uploads, file);
                    zip.CreateEntryFromFile(file, Path.Combine("uploads", entryName).Replace('\\', '/'));
                }
            }

            var note = zip.CreateEntry("readme.txt");
            await using var writer = new StreamWriter(note.Open());
            await writer.WriteLineAsync($"Yousuf Enterprise local backup created {DateTime.UtcNow:u} ({trigger}).");
            await writer.WriteLineAsync("Google Drive upload is not configured. Place Drive credentials to enable cloud sync.");
        }

        var info = new FileInfo(path);
        var log = new BackupLog
        {
            Timestamp = DateTime.UtcNow,
            FileSizeBytes = info.Length.ToString(),
            UploadStatus = "LocalSuccess",
            DriveFileId = null,
            Notes = $"Archive {fileName}. Cloud upload pending Drive configuration."
        };
        _db.BackupLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }
}

public class DailyBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyBackupHostedService> _logger;

    public DailyBackupHostedService(IServiceScopeFactory scopeFactory, ILogger<DailyBackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = TimeSpan.FromHours(24);
                await Task.Delay(delay, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
                await backup.RunBackupAsync("scheduled");
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed.");
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.BackupLogs.Add(new BackupLog
                {
                    Timestamp = DateTime.UtcNow,
                    FileSizeBytes = "0",
                    UploadStatus = "Failed",
                    Notes = ex.Message
                });
                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
