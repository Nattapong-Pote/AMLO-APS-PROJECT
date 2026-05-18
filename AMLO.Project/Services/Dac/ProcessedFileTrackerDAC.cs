using AMLO.Project.Models;
using AMLO.Project.Services.SurrealDbProvider;
using SurrealDb.Net;
using SurrealDb.Net.Models;

namespace AMLO.Project.Services.Dac;

public interface IProcessedFileTrackerDAC
{
    /// <summary>
    /// Check if file has already been processed successfully
    /// </summary>
    Task<bool> GetByFileName(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log file processing status with unified interface
    /// </summary>
    /// <param name="fileName">The name of the file being processed</param>
    /// <param name="status">The processing status (Success, Duplicate, Fail)</param>
    /// <param name="recordCount">Number of records processed (for Success status)</param>
    /// <param name="notes">Additional details about the processing result</param>
    /// <param name="fileHash">Optional file hash for integrity verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateLog(string fileName, LogStatus status, int recordCount = 0, string notes = null, string fileHash = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all processed file records
    /// </summary>
    Task<IEnumerable<ProcessedFileRecord>> GetAll(CancellationToken cancellationToken = default);
}

public class ProcessedFileTrackerDAC : IProcessedFileTrackerDAC
{
    private readonly IDbProvider<ProcessedFileRecord, ProcessedFileRecord> _dbProvider;

    public ProcessedFileTrackerDAC(SurrealDbProviderFactoryBase surrealDbProviderFactory)
    {
        _dbProvider = surrealDbProviderFactory.Create<ProcessedFileRecord, ProcessedFileRecord>();
    }

    public async Task<bool> GetByFileName(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var escapedFileName = EscapeSurrealQuery(fileName);
            FormattableString query = $@"
                SELECT FileName, Status FROM processed_files
                WHERE FileName = $fileName 
                AND Status = 'Success'
                LIMIT 1
            ";

            var parameters = new Dictionary<string, object?>
            {
                { "fileName", escapedFileName }
            };
            var result = await _dbProvider.Query<ProcessedFileRecord>(query, parameters, cancellationToken);

            return result != null && result.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateLog(string fileName, LogStatus status, int recordCount = 0, string notes = null, string fileHash = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        try
        {
            var statusString = MapLogStatusToString(status);
            var finalNotes = notes ?? GetDefaultNotes(status, recordCount);

            var record = new ProcessedFileRecord
            {
                FileName = fileName,
                FileHash = fileHash,
                ProcessedDateTime = DateTime.UtcNow,
                RecordCount = recordCount,
                Status = statusString,
                Notes = finalNotes
            };

            // Set RecordId before upsert (required by SurrealDB)
            // Use FileName as unique identifier in the processed_files table
            if (record.Id == null || record.Id.Equals(default(RecordId)))
            {
                record.Id = RecordId.From("processed_files", Guid.NewGuid().ToString("N"));
            }

            await _dbProvider.Upsert(record, cancellationToken);

            var logLevel = status == LogStatus.Success ? "[SUCCESS]" : status == LogStatus.Duplicate ? "[SKIPPED]" : "[FAILED]";
            Console.WriteLine($"{logLevel} File '{fileName}' logged with status {statusString}: {finalNotes}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to log file status for '{fileName}': {ex.Message}");
            throw; // Re-throw so caller knows about the error
        }
    }

    public async Task<IEnumerable<ProcessedFileRecord>> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            FormattableString query = $@"
                SELECT * FROM processed_files
            ";

            var result = await _dbProvider.Query<ProcessedFileRecord>(query, null, cancellationToken);

            return result ?? new List<ProcessedFileRecord>();
        }
        catch
        {
            return new List<ProcessedFileRecord>();
        }
    }

    private string MapLogStatusToString(LogStatus status) => status switch
    {
        LogStatus.Success => "Success",
        LogStatus.Duplicate => "Skipped",
        LogStatus.Fail => "Failed",
        _ => "Unknown"
    };

    private string GetDefaultNotes(LogStatus status, int recordCount) => status switch
    {
        LogStatus.Success => $"Successfully processed {recordCount} records",
        LogStatus.Duplicate => "Duplicate file - already processed and saved to database",
        LogStatus.Fail => "Processing failed - see error details in logs",
        _ => "Unknown status"
    };

    private string EscapeSurrealQuery(string input)
    {
        return input?.Replace("'", "''") ?? string.Empty;
    }
}
