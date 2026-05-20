using AMLO.Project.Extensions;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using AMLO.Project.Services.SurrealDbProvider;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;
using Azure.Identity;

namespace AMLO.Project;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== AMLO Integration System ===\n");

        var builder = WebApplication.CreateBuilder(args);

        // ตรวจสอบว่าเป็น Environment ไหน (ถ้าเป็น Production หรือมี URI ค่อยเปิดใช้ Key Vault)
        if (builder.Environment.IsProduction() || builder.Environment.IsDevelopment())
        {
            var keyVaultUri = "https://amlo-kv.vault.azure.net/";
        
            // แนะนำใช้ DefaultAzureCredential ซึ่งปลอดภัยและยืดหยุ่นสูง:
            // - บน Local (Development): จะใช้ Identity จาก Azure CLI, VS Code หรือ Visual Studio ที่คุณ Login ไว้
            // - บน Production: จะใช้ Managed Identity ของ Azure App Service / VM ได้ทันทีโดยไม่ต้องใส่ Client Secret ในโค้ด
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();

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

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        // Register AMLO services
        builder.Services.AddAmloProject(
            dbUrl: dbUrl,
            user: dbUser,
            pass: dbPass,
            ns: dbNamespace,
            db: dbDatabase
        );

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        try
        {
            // Step 2: Initialize Database
            Console.WriteLine("[STEP] Initializing database...");
            var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await dbInitializer.InitializeAsync();
            Console.WriteLine("[✓] Database initialized!\n");
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

        host.UseSwagger();
        host.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AMLO Integration API v1");
            options.RoutePrefix = string.Empty;
        });

        host.MapControllers();
        host.Run();
    }
}
