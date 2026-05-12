using SurrealDb.Net;
using System;
using System.Threading.Tasks;

namespace AMLO.Project.Services
{
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
    }

    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly ISurrealDbClient _dbClient;
        private const string AmloTableName = "amlo_master";

        public DatabaseInitializer(ISurrealDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("[*] Initializing SurrealDB...");
                Console.WriteLine($"[*] Verifying table '{AmloTableName}' exists...");

                try
                {
                    // โ… Simple test: try to select from table
                    var records = await _dbClient.Select<dynamic>(AmloTableName, default);
                    Console.WriteLine($"[OK] Table '{AmloTableName}' exists and is accessible!");
                }
                catch (Exception ex) when (ex.Message.Contains("Cannot find") || ex.Message.Contains("not found"))
                {
                    Console.WriteLine($"[WARN] Table '{AmloTableName}' not found");
                    Console.WriteLine($"[*] Table will be created on first insert");
                }

                Console.WriteLine("[OK] Database initialization completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Database initialization warning: {ex.Message}");
            }
        }
    }
}
