using AMLO.Project.Models;
using AMLO.Project.Services.SurrealDbProvider;
using Azure;
using CsvHelper;
using Mapster;
using SurrealDb.Net;
using SurrealDb.Net.Models;

namespace AMLO.Project.Services.Dac;

public interface IProcessDataServiceDAC
{
    /// <summary>
    /// Upsert data to amlo_master (without history management)
    /// Use this after you've already handled archiving separately
    /// </summary>
    Task Upsert(AmloDto dtoData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if TypeName exists in amlo_master (active, non-archived records)
    /// </summary>
    Task<bool> GetByType(string typeName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive all records with specific TypeName from amlo_master to amlo_history
    /// Use this ONCE per file before processing any records
    /// </summary>
    Task DeleteByType(string typeName, CancellationToken cancellationToken = default);
}

public class ProcessDataServiceDAC : IProcessDataServiceDAC
{
    private readonly IDbProvider<AmloDbEntity, AmloDto> _dbProvider;

    public ProcessDataServiceDAC(SurrealDbProviderFactoryBase surrealDbProviderFactory)
    {
        _dbProvider = surrealDbProviderFactory.Create<AmloDbEntity, AmloDto>();
    }

    /// <summary>
    /// Simple create/upsert to amlo_master with CreatedAt and IsArchived flags
    /// </summary>
    public async Task Upsert(AmloDto dtoData, CancellationToken cancellationToken = default)
    {
        if (dtoData == null)
            throw new ArgumentNullException(nameof(dtoData));

        var entity = dtoData.Adapt<AmloDbEntity>();

        // Ensure ID is set (mapped from EntityId during Adapt)
        if (entity.Id == null || entity.Id.Equals(default(RecordId)))
        {
            entity.Id = RecordId.From("amlo_master", $"{dtoData.TypeName}_{dtoData.EntityId}");
        }

        entity.IsArchived = false;
        entity.ArchivedAt = null;
        entity.CreatedAt = DateTime.UtcNow;

        await _dbProvider.Upsert(entity, cancellationToken);
    }

    /// <summary>
    /// Check if TypeName exists in amlo_master (only non-archived records)
    /// Returns true if at least one record exists
    /// </summary>
    public async Task<bool> GetByType(string typeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        try
        {
            FormattableString query = $@"
                SELECT id FROM amlo_master 
                WHERE TypeName = $typeName AND IsArchived = false
                LIMIT 1
            ";

            var parameters = new Dictionary<string, object?>
            {
                { "typeName", typeName }
            };

            var result = await _dbProvider.Query(query, parameters, cancellationToken);
            var records = result.GetValue<List<object>>(0);
            return records != null && records.Any();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Error checking TypeName existence: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Archive ALL records with specific TypeName from amlo_master to amlo_history
    /// Uses atomic SurrealDB transaction to ensure data consistency
    /// 
    /// Flow:
    /// 1. Insert all active records of this TypeName into amlo_history
    /// 2. Mark them as archived in amlo_master (or delete, depending on your policy)
    /// 
    /// Example:
    /// - File 1: taliban_9FDE9AA2AA64_070526 (100 records stored)
    /// - File 2: taliban_9FDE9AA2AA65_070526 (120 records)
    /// When File 2 is processed:
    /// - All 100 records from File 1 move to amlo_history
    /// - Then 120 new records from File 2 are inserted into amlo_master
    /// </summary>
    public async Task DeleteByType(string typeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new ArgumentException("TypeName cannot be empty", nameof(typeName));

        // Use SurrealDB atomic transaction for data safety
        FormattableString query = $@"
            BEGIN TRANSACTION;

            -- Copy all active records to amlo_history with ArchivedAt timestamp
            INSERT INTO amlo_history 
                SELECT 
                    TypeName,
                    Version,
                    Data,
                    CreatedAt,
                    $archivedAt AS ArchivedAt,
                    true AS IsArchived
                FROM amlo_master 
                WHERE TypeName = $typeName AND IsArchived = false;

            -- Delete from amlo_master (move operation)
            DELETE FROM amlo_master 
            WHERE TypeName = $typeName AND IsArchived = false;

            COMMIT TRANSACTION;
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "typeName", typeName },
            { "archivedAt", DateTime.UtcNow }
        };

        try
        {
            await _dbProvider.Query(query, parameters, cancellationToken);
            Console.WriteLine($"[OK] Successfully archived TypeName '{typeName}' to amlo_history");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to archive TypeName '{typeName}': {ex.Message}");
            throw;
        }
    }
}
