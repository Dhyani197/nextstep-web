using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextStepWeb.Data;
using NextStepWeb.Services.Implementations;
using NextStepWeb.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Ensure Database folder exists for physical SQL Server MDF file
var dbFolder = Path.Combine(builder.Environment.ContentRootPath, "Database");
if (!Directory.Exists(dbFolder))
{
    Directory.CreateDirectory(dbFolder);
}
AppDomain.CurrentDomain.SetData("DataDirectory", dbFolder);

// Resolve physical MDF absolute path reliably regardless of CWD or username
var mdfPath = Path.GetFullPath(Path.Combine(dbFolder, "NextStep.mdf"));

// Configure Entity Framework Core with SQL Server LocalDB and physical MDF
var rawConnStr = builder.Configuration.GetConnectionString("NextStepDb")
    ?? $@"Server=(localdb)\MSSQLLocalDB;AttachDbFilename={mdfPath};Database=NextStep;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;";

var connectionString = rawConnStr.Replace("|DataDirectory|", dbFolder);

builder.Services.AddDbContext<NextStepDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null);
    });
});

// Configure Services and HTTP Client
builder.Services.AddHttpClient<INextStepApiService, NextStepApiService>();
builder.Services.AddScoped<ISituationService, SituationService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure Database and physical MDF are initialized using EF Core Migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<NextStepDbContext>();
        logger.LogInformation("Applying EF Core migrations to physical SQL Server MDF at: {MdfPath}", mdfPath);
        db.Database.Migrate();
        logger.LogInformation("Database migration completed successfully. Required tables are verified.");
    }
    catch (Microsoft.Data.SqlClient.SqlException sqlEx)
    {
        string diagnostic = sqlEx.Number switch
        {
            52 or 2 => "SQL Server LocalDB Runtime is not installed on this machine (Error 52/2: Local Database Runtime not found).",
            26 => "SQL Server LocalDB instance 'MSSQLLocalDB' is not running or not found (Error 26).",
            5120 or 5105 => $"SQL Server cannot attach physical MDF file at '{mdfPath}'. Check folder permissions and disk locks (Error {sqlEx.Number}).",
            18456 => "SQL Server login authentication failed (Error 18456).",
            _ => $"SQL Server error (Code {sqlEx.Number}, State {sqlEx.State}): {sqlEx.Message}"
        };
        logger.LogError(sqlEx, "Database Initialization Failed: {Diagnostic}. Connection String: {ConnStr}", diagnostic, connectionString);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during EF Core migration: {Message}", ex.Message);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Situation}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
