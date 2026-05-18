using AMLO.Project.Extensions;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using AMLO.Project.Services.SurrealDbProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;

namespace AMLO.Project;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== AMLO Integration System ===\n");

        // Configuration
        const string dbUrl = "http://127.0.0.1:8000";
        const string dbUser = "root";
        const string dbPass = "root";
        const string dbNamespace = "ns";
        const string dbDatabase = "db";
        const string csvBlobPattern = "*.csv";

        Console.WriteLine($"[CONFIG] Database URL: {dbUrl}");
        Console.WriteLine($"[CONFIG] Namespace: {dbNamespace} | Database: {dbDatabase}");
        Console.WriteLine($"[CONFIG] Blob/File Pattern: {csvBlobPattern}\n");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        // Register AMLO services
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
            // Step 0: Bootstrap - Authenticate with SurrealDB immediately
            Console.WriteLine("[STEP 0] Bootstrapping SurrealDB authentication...");
            var dbClient = scope.ServiceProvider.GetRequiredService<ISurrealDbClient>();
            var authContext = scope.ServiceProvider.GetRequiredService<SurrealDbAuthContext>();

            await AuthenticateDbClient(dbClient, authContext);
            Console.WriteLine("[✓] SurrealDB authenticated and context set!\n");

            // Step 1: Verify ISurrealDbClient registration
            Console.WriteLine("[STEP 1] Verifying ISurrealDbClient registration...");
            if (dbClient == null)
            {
                throw new InvalidOperationException("ISurrealDbClient is NOT registered!");
            }
            Console.WriteLine("[✓] ISurrealDbClient verified!\n");

            // Step 2: Initialize Database
            Console.WriteLine("[STEP 2] Initializing database...");
            var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await dbInitializer.InitializeAsync();
            Console.WriteLine("[✓] Database initialized!\n");

            // Step 3: Prepare CSV import
            Console.WriteLine($"[STEP 3] Preparing to import CSV with pattern: {csvBlobPattern}");

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
    }

    /// <summary>
    /// Authenticates the SurrealDB client with provided credentials and sets namespace/database context.
    /// This must be called before any CRUD operations to ensure proper permissions.
    /// </summary>
    private static async Task AuthenticateDbClient(ISurrealDbClient dbClient, SurrealDbAuthContext authContext)
    {
        try
        {
            // Sign in with root credentials
            Console.WriteLine($"[*] Signing in with user '{authContext.Username}'...");
            await dbClient.SignIn(new RootAuth
            {
                Username = authContext.Username,
                Password = authContext.Password
            });
            Console.WriteLine($"[✓] Authentication successful!");

            // Set namespace and database context
            Console.WriteLine($"[*] Setting namespace '{authContext.Namespace}' and database '{authContext.Database}'...");
            await dbClient.Use(authContext.Namespace, authContext.Database);
            Console.WriteLine($"[✓] Namespace and database context set!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[✗] Authentication failed: {ex.Message}");
            throw new InvalidOperationException(
                $"Failed to authenticate with SurrealDB at {dbClient}. " +
                $"Ensure SurrealDB is running and credentials are correct.", ex);
        }
    }
}
