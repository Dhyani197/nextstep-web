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

// Ensure App_Data folder exists for physical SQL Server MDF file
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
if (!Directory.Exists(dataDir))
{
    Directory.CreateDirectory(dataDir);
}
AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);

// Configure Entity Framework Core with SQL Server LocalDB and physical MDF
var connectionString = builder.Configuration.GetConnectionString("NextStepDb")
    ?? $@"Server=(localdb)\MSSQLLocalDB;AttachDbFilename={Path.Combine(dataDir, "NextStepDb.mdf")};Initial Catalog=NextStepDb;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;";

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

// Ensure Database and physical MDF are initialized
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<NextStepDbContext>();
        logger.LogInformation("Initializing database with physical MDF file at {DataDir}", dataDir);
        db.Database.EnsureCreated();
        logger.LogInformation("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization note: SQL Server LocalDB connection could not be established immediately. If LocalDB is not yet installed on this machine, please run the installer command provided in README.");
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
