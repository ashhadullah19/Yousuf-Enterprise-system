using System.IO.Compression;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
            async Task WriteTableAsync<T>(string name, IQueryable<T> query)
            {
                var rows = await query.ToListAsync();
                var entry = zip.CreateEntry($"data/{name}.json");
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(stream, rows, JsonOptions);
            }

            // IgnoreQueryFilters so soft-deleted parties/products are still included — a backup
            // that silently drops "deleted" rows isn't a real backup.
            await WriteTableAsync("SystemSettings", _db.SystemSettings.AsNoTracking());
            await WriteTableAsync("Parties", _db.Parties.IgnoreQueryFilters().AsNoTracking());
            await WriteTableAsync("Products", _db.Products.IgnoreQueryFilters().AsNoTracking());
            await WriteTableAsync("OwnedPurchases", _db.OwnedPurchases.AsNoTracking());
            await WriteTableAsync("ConsignmentReceipts", _db.ConsignmentReceipts.AsNoTracking());
            await WriteTableAsync("SalesInvoices", _db.SalesInvoices.AsNoTracking());
            await WriteTableAsync("SalesInvoiceAllocations", _db.SalesInvoiceAllocations.AsNoTracking());
            await WriteTableAsync("LedgerEntries", _db.LedgerEntries.AsNoTracking());
            await WriteTableAsync("BankAccounts", _db.BankAccounts.AsNoTracking());
            await WriteTableAsync("BankTransactions", _db.BankTransactions.AsNoTracking());
            await WriteTableAsync("ExpenseTypes", _db.ExpenseTypes.AsNoTracking());
            await WriteTableAsync("Expenses", _db.Expenses.AsNoTracking());
            await WriteTableAsync("DastiEntries", _db.DastiEntries.AsNoTracking());
            await WriteTableAsync("RolePermissions", _db.RolePermissions.AsNoTracking());

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
            await writer.WriteLineAsync($"Yousuf Enterprise backup created {DateTime.UtcNow:u} ({trigger}).");
            await writer.WriteLineAsync("Contains: data/*.json (full table exports, including soft-deleted rows) and uploads/ (uploaded files).");
            var driveLink = (await _db.SystemSettings.AsNoTracking().FirstAsync()).GoogleDriveFolderLink;
            await writer.WriteLineAsync(string.IsNullOrWhiteSpace(driveLink)
                ? "No Google Drive folder configured in Settings — download this backup from the Backups page."
                : $"Automatic Google Drive upload isn't configured yet — download this backup from the Backups page and upload it to: {driveLink}");
        }

        var info = new FileInfo(path);
        var log = new BackupLog
        {
            Timestamp = DateTime.UtcNow,
            FileName = fileName,
            FileSizeBytes = info.Length.ToString(),
            UploadStatus = "LocalSuccess",
            DriveFileId = null,
            Notes = $"Archive {fileName} ({info.Length / 1024} KB). Download from the Backups page — automatic Drive upload not yet configured."
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
