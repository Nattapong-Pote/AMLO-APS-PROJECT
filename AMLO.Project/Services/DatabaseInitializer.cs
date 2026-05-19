using AMLO.Project.Controllers;
using Microsoft.Extensions.Logging;
using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;
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
        private readonly ISurrealDbSession _dbClient;
        private const string AmloMasterTableName = "amlo_master";
        private const string AmloHistoryTableName = "amlo_history";
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            ISurrealDbSession dbClient,
            ILogger<DatabaseInitializer> logger)
        {
            _dbClient = dbClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SurrealDB...");

                // Initialize amlo_master table
                await InitializeTableAsync(AmloMasterTableName);

                // Initialize amlo_history table
                await InitializeTableAsync(AmloHistoryTableName);

                _logger.LogInformation("[OK] Database initialization completed!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Database initialization failed: {ex.Message}");
                throw;
            }
        }

        private async Task InitializeTableAsync(string tableName)
        {
            try
            {
                _logger.LogInformation($"[*] Verifying table '{tableName}' exists...");

                // Simple test: try to select from table
                var records = await _dbClient.Select<dynamic>(tableName, default);
                _logger.LogInformation($"[OK] Table '{tableName}' exists and is accessible!");
            }
            catch (Exception ex) when (ex.Message.Contains("Cannot find") || ex.Message.Contains("not found"))
            {
                _logger.LogWarning($"[WARN] Table '{tableName}' not found, creating schema...");

                try
                {
                    var defineQuery = GetTableDefinitionQuery(tableName);
                    await _dbClient.RawQuery(defineQuery, default);
                    _logger.LogInformation($"[OK] Table '{tableName}' schema defined!");
                }
                catch (Exception defineEx)
                {
                    _logger.LogInformation($"[INFO] Table '{tableName}' will be auto-created on first insert: {defineEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Failed to initialize table '{tableName}': {ex.Message}");
                throw;
            }
        }

        private static string GetTableDefinitionQuery(string tableName)
        {
            // Define schema for both amlo_master and amlo_history tables
            // SCHEMALESS allows flexibility, but DEFINE FIELD ensures type consistency
            // PERMISSIONS use (1 = 1) to ALLOW all authenticated users
            return $@"
                DEFINE TABLE {tableName} SCHEMALESS
                PERMISSIONS
                    FOR create ALLOW (1 = 1)
                    FOR read ALLOW (1 = 1)
                    FOR update ALLOW (1 = 1)
                    FOR delete ALLOW (1 = 1);

                DEFINE FIELD TypeName ON TABLE {tableName} TYPE string ASSERT string::len($value) > 0;
                DEFINE FIELD Version ON TABLE {tableName} TYPE string ASSERT string::len($value) > 0;
                DEFINE FIELD Data ON TABLE {tableName} TYPE object;
                DEFINE FIELD CreatedAt ON TABLE {tableName} TYPE datetime VALUE time::now();
                DEFINE FIELD ArchivedAt ON TABLE {tableName} TYPE datetime OPTIONAL;
                DEFINE FIELD IsArchived ON TABLE {tableName} TYPE bool DEFAULT false;

                DEFINE INDEX idx_typename_active ON TABLE {tableName} COLUMNS TypeName, IsArchived;
                DEFINE INDEX idx_archived_at ON TABLE {tableName} COLUMNS ArchivedAt;
            ";
        }
    }
}
