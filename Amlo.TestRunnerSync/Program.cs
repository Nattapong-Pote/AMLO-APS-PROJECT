using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AMLO.Project.Extensions;
using SurrealDb.Net;
using System;
using Microsoft.Extensions.Configuration;

Console.WriteLine("=== Starting AMLO Sync Test ===\n");

// 🔧 Configuration
const string dbUrl = "http://127.0.0.1:8000";
const string dbUser = "root";
const string dbPass = "root";
const string dbNamespace = "ns";
const string dbDatabase = "db";
//const string csvFilePath = @"D:\Knowledge\CFR\DATA\TEST V3\al-qaida\Test_Output_Single.csv";
// ✅ CHANGED: Use wildcard pattern instead of hardcoded filename
const string csvBlobPattern = "*.csv";

Console.WriteLine($"[CONFIG] Database URL: {dbUrl}");
Console.WriteLine($"[CONFIG] Namespace: {dbNamespace} | Database: {dbDatabase}");
Console.WriteLine($"[CONFIG] Blob/File Pattern: {csvBlobPattern}\n");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration); // <--- สำคัญมาก!

// Register Services
builder.Services.AddAmloProject(
    dbUrl: dbUrl,
    user: dbUser,
    pass: dbPass,
    ns: dbNamespace,
    db: dbDatabase
);

builder.Services.AddScoped<DataProcessingService>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

try
{
    // Step 1: Check ISurrealDbClient
    Console.WriteLine("[STEP 1] Checking if ISurrealDbClient is registered...");
    var dbClient = scope.ServiceProvider.GetService<ISurrealDbClient>();
    if (dbClient == null)
    {
        throw new InvalidOperationException("ISurrealDbClient is NOT registered!");
    }
    Console.WriteLine("[✓] ISurrealDbClient registered!\n");

    // ✅ NEW Step 1.5: Authenticate with SurrealDB
    Console.WriteLine("[STEP 1.5] Authenticating with SurrealDB...");
    try
    {
        // ✅ Sign in as root user - try RootAuth first
        await dbClient.SignIn(new SurrealDb.Net.Models.Auth.RootAuth
        { 
            Username = dbUser, 
            Password = dbPass 
        });
        Console.WriteLine("[✓] Sign in successful!\n");

        // ✅ Set namespace and database context
        Console.WriteLine("[STEP 1.6] Setting namespace and database context...");
        await dbClient.Use(dbNamespace, dbDatabase);
        Console.WriteLine("[✓] Namespace and database set!\n");
    }
    catch (Exception authEx)
    {
        Console.WriteLine($"[WARN] Authentication attempt: {authEx.Message}");
        Console.WriteLine("[*] Continuing to initialize step...\n");
    }

    // Step 2: Initialize Database
    Console.WriteLine("[STEP 2] Initializing database...");
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await dbInitializer.InitializeAsync();
    Console.WriteLine("[✓] Database initialized!\n");

    // Step 3: ไม่ต้องเช็คไฟล์ local อีกต่อไป
    Console.WriteLine($"[STEP 3] Preparing to import CSV file with pattern: {csvBlobPattern}");

    // Step 4: Resolve DataProcessingService
    Console.WriteLine($"[STEP 4] Resolving DataProcessingService...");
    var processService = scope.ServiceProvider.GetRequiredService<DataProcessingService>();
    Console.WriteLine($"[✓] DataProcessingService resolved!\n");

    // Step 5: Import CSV
    Console.WriteLine($"[STEP 5] Starting CSV import...");
    Console.WriteLine($"[*] Reading and processing CSV records...\n");
    await processService.ImportCsvAsync(csvBlobPattern);

    Console.WriteLine($"\n[✅] SUCCESS! All tasks completed!");
    Console.WriteLine($"    CSV data has been imported to SurrealDB table 'amlo_master'");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"\n[✗] {ex.GetType().Name}: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[✗] {ex.GetType().Name}: {ex.Message}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"[InnerException] {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
    
    if (!string.IsNullOrEmpty(ex.StackTrace))
    {
        Console.WriteLine($"\n[StackTrace]\n{ex.StackTrace}");
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();