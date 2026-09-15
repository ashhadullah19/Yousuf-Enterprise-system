using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

// Writes to console *and* a file under the App Service persistent storage (%HOME%\LogFiles),
// so a startup crash is captured even if Azure's "Application Logging" / stdout capture
// isn't turned on in the portal and Log Stream / Kudu show nothing.
LogBootstrap("Process starting.");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.AddAzureWebAppDiagnostics();
    builder.Services.Configure<Microsoft.Extensions.Logging.AzureAppServices.AzureFileLoggerOptions>(options =>
    {
        options.FileName = "app-";
        options.FileSizeLimit = 10 * 1024 * 1024;
        options.RetainedFileCountLimit = 5;
    });

    LogBootstrap($"Environment: {builder.Environment.EnvironmentName}, ContentRoot: {builder.Environment.ContentRootPath}");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    LogBootstrap($"DefaultConnection configured: {(string.IsNullOrWhiteSpace(connectionString) ? "NO (empty!)" : "yes")}, Server hint: {MaskConnectionString(connectionString)}");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddIdentity<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
        options.Filters.Add<Yousuf_Enterprise_system.Filters.ModalRequestFilter>();
    });

    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IDocumentNumberService, DocumentNumberService>();
    builder.Services.AddScoped<IInvoiceService, InvoiceService>();
    builder.Services.AddScoped<ILedgerService, LedgerService>();
    builder.Services.AddScoped<IExportService, ExportService>();
    builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
    builder.Services.AddScoped<IBackupService, BackupService>();
    builder.Services.AddHostedService<DailyBackupHostedService>();

    LogBootstrap("Services configured. Building app.");
    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("App built. Environment={Environment}", app.Environment.EnvironmentName);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                if (exceptionFeature?.Error is { } ex)
                {
                    logger.LogError(ex, "Unhandled exception on {Path}", exceptionFeature.Path);
                }

                context.Response.Redirect("/Home/Error");
                await Task.CompletedTask;
            });
        });
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    logger.LogInformation("Applying database migrations...");
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed. Check ConnectionStrings:DefaultConnection (currently: {MaskedConnection}) and that the database server is reachable from this App Service.", MaskConnectionString(connectionString));
            LogBootstrap($"FATAL during DB migration: {ex}");
            throw;
        }

        try
        {
            logger.LogInformation("Seeding identity data...");
            await IdentitySeeder.SeedAsync(scope.ServiceProvider);
            logger.LogInformation("Identity seed complete.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Identity/data seeding failed.");
            LogBootstrap($"FATAL during seeding: {ex}");
            throw;
        }
    }

    logger.LogInformation("Startup complete. Starting web host...");
    LogBootstrap("Startup complete. Calling app.Run().");
    app.Run();
}
catch (HostAbortedException)
{
    // Expected: `dotnet ef` builds the host only to inspect the DbContext, then aborts it.
    // Not a real startup failure, so it shouldn't be logged as one.
    throw;
}
catch (Exception ex)
{
    LogBootstrap($"FATAL startup exception: {ex}");
    Console.Error.WriteLine($"[FATAL STARTUP ERROR] {ex}");
    throw;
}

static string MaskConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "(none)";
    }

    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var safeParts = parts
        .Where(p => !p.StartsWith("password", StringComparison.OrdinalIgnoreCase)
                 && !p.StartsWith("pwd", StringComparison.OrdinalIgnoreCase)
                 && !p.StartsWith("user id", StringComparison.OrdinalIgnoreCase)
                 && !p.StartsWith("uid", StringComparison.OrdinalIgnoreCase));
    return string.Join(';', safeParts);
}

static void LogBootstrap(string message)
{
    var line = $"{DateTime.UtcNow:u} {message}";
    try
    {
        Console.WriteLine(line);
    }
    catch
    {
        // ignore - console may not be available
    }

    try
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var dir = Path.Combine(home, "LogFiles", "Application");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup-bootstrap.log"), line + Environment.NewLine);
        }
    }
    catch
    {
        // best-effort only - never let logging crash startup
    }
}
