using AMLO.Project.Models;
using Azure;
using SurrealDb.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AMLO.Project.Services.Dac
{
    public interface IProcessedFileTrackerDAC
    {
        /// <summary>
        /// ตรวจสอบว่าไฟล์เคยประมวลผลสำเร็จหรือไม่
        /// </summary>
        Task<bool> IsFileAlreadyProcessedAsync(string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// บันทึกประวัติการประมวลผลไฟล์สำเร็จ
        /// </summary>
        Task RecordFileProcessedAsync(string fileName, int recordCount, string fileHash = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// บันทึกประวัติการประมวลผลไฟล์ที่ข้ามไป (Duplicate)
        /// </summary>
        Task RecordFileSkippedAsync(string fileName, string reason = "Duplicate file", CancellationToken cancellationToken = default);

        /// <summary>
        /// บันทึกประวัติการประมวลผลไฟล์ที่ล้มเหลว
        /// </summary>
        Task RecordFileFailedAsync(string fileName, string errorMessage, CancellationToken cancellationToken = default);

        /// <summary>
        /// รับประวัติการประมวลผลทั้งหมด
        /// </summary>
        Task<IEnumerable<ProcessedFileRecord>> GetAllProcessedFilesAsync(CancellationToken cancellationToken = default);
    }

    public class ProcessedFileTrackerDAC : IProcessedFileTrackerDAC
    {
        private readonly ISurrealDbClient _dbClient;

        public ProcessedFileTrackerDAC(ISurrealDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task<bool> IsFileAlreadyProcessedAsync(string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                var escapedFileName = EscapeSurrealQuery(fileName);
                var response = await _dbClient.Query(
                    $"SELECT FileName, Status FROM processed_files WHERE FileName = {escapedFileName} AND Status = 'Success' LIMIT 1;",
                    cancellationToken
                );

                var records = response.GetValue<List<ProcessedFileRecord>>(0);

                return records != null && records.Any();
            }
            catch
            {
                //ถ้าไม่เจอ หรือ error ให้ถือว่าไฟล์ยังไม่เคยประมวลผล
                return false;
            }
        }

        public async Task RecordFileProcessedAsync(string fileName, int recordCount, string fileHash = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var record = new ProcessedFileRecord
                {
                    FileName = fileName,
                    FileHash = fileHash,
                    ProcessedDateTime = DateTime.UtcNow,
                    RecordCount = recordCount,
                    Status = "Success",
                    Notes = "Successfully processed"
                };

                await _dbClient.Upsert("processed_files", record, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessedFileTracker] Error recording processed file: {ex.Message}");
            }
        }

        public async Task RecordFileSkippedAsync(string fileName, string reason = "Duplicate file", CancellationToken cancellationToken = default)
        {
            try
            {
                var record = new ProcessedFileRecord
                {
                    FileName = fileName,
                    ProcessedDateTime = DateTime.UtcNow,
                    RecordCount = 0,
                    Status = "Skipped",
                    Notes = reason
                };

                await _dbClient.Upsert("processed_files", record, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessedFileTracker] Error recording skipped file: {ex.Message}");
            }
        }

        public async Task RecordFileFailedAsync(string fileName, string errorMessage, CancellationToken cancellationToken = default)
        {
            try
            {
                var record = new ProcessedFileRecord
                {
                    FileName = fileName,
                    ProcessedDateTime = DateTime.UtcNow,
                    RecordCount = 0,
                    Status = "Failed",
                    Notes = errorMessage
                };

                await _dbClient.Upsert("processed_files", record, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessedFileTracker] Error recording failed file: {ex.Message}");
            }
        }

        public async Task<IEnumerable<ProcessedFileRecord>> GetAllProcessedFilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // SurrealDB Query ไม่สนับสนุน generic type parameter
                // ต้องใช้ Dictionary แทนแล้ว cast ไป object
                var result = await _dbClient.Query($"SELECT * FROM processed_files;", cancellationToken);
                return result?.Cast<ProcessedFileRecord>() ?? new List<ProcessedFileRecord>();
            }
            catch
            {
                return new List<ProcessedFileRecord>();
            }
        }

        private string EscapeSurrealQuery(string input)
        {
            // Escape single quotes สำหรับ SurrealDB query
            return input?.Replace("'", "''") ?? string.Empty;
        }
    }
}
