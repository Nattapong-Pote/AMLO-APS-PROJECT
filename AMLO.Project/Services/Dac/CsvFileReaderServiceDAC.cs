using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using System;

namespace AMLO.Project.Services.Dac
{
    public interface ICsvFileReaderServiceDAC
    {
        /// <summary>
        /// อ่านไฟล์ CSV จาก Local หรือ Azure Blob Storage โดยอัตโนมัติ (ขึ้นอยู่กับการตั้งค่า)
        /// </summary>
        Task<IEnumerable<IDictionary<string, string>>> ReadCsvAsync(string filePath, CancellationToken cancellationToken = default);
    }

    public class LocalCsvFileReaderService : ICsvFileReaderServiceDAC
    {
        public async Task<IEnumerable<IDictionary<string, string>>> ReadCsvAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var records = new List<IDictionary<string, string>>();
            if (!File.Exists(filePath))
                return records;

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            if (headers == null) return records;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dict = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    dict[header] = csv.GetField(header);
                }
                records.Add(dict);
            }
            return records;
        }
    }

    public class AzureBlobCsvFileReaderService : ICsvFileReaderServiceDAC
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _logFolderPath;
        private readonly string _filePattern;

        public AzureBlobCsvFileReaderService(IConfiguration config)
        {
            _connectionString = config["CsvFileReader:Azure:ConnectionString"];
            _containerName = config["CsvFileReader:Azure:ContainerName"];
            _filePattern = config["CsvFileReader:Azure:FilePattern"] ?? "*.csv";
            _logFolderPath = config["CsvFileReader:Log:FolderPath"] ?? "CsvReadLogs";
        }

        /// <summary>
        /// ค้นหาไฟล์ CSV ทั้งหมดที่ตรงกับ pattern อัตโนมัติ (แก้ไขให้คืนค่าเป็น List)
        /// </summary>
        private async Task<List<string>> FindBlobFileAsync(BlobContainerClient containerClient, string filePathOrPattern, CancellationToken cancellationToken)
        {
            var matchedFiles = new List<string>();
            // ถ้าเป็น wildcard pattern ให้ค้นหา
            if (filePathOrPattern.Contains("*") || filePathOrPattern.Contains("?"))
            {
                // แปลง wildcard pattern เป็น regex
                var regexPattern = "^" + Regex.Escape(filePathOrPattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    if (regex.IsMatch(blobItem.Name))
                    {
                        matchedFiles.Add(blobItem.Name);  // ✅ Add ชื่อไฟล์ที่พบจริงลงใน list
                    }
                }
                return matchedFiles;  // คืนค่า list ของไฟล์ที่ตรงกับ pattern
            }
            else
            {
                // ถ้าเป็นชื่อไฟล์ที่ชัดเจน (ไม่มี wildcard) ให้เพิ่มเข้าไปเลย
                matchedFiles.Add(filePathOrPattern);
            }

            return matchedFiles;
        }

        /// <summary>
        /// ค้นหาชื่อไฟล์ทั้งหมดที่พบจริง (อัปเดตให้รองรับ List)
        /// </summary>
        public async Task<List<string>> GetActualBlobNamesAsync(string filePathOrPattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                var foundName = await FindBlobFileAsync(containerClient, filePathOrPattern, cancellationToken);
                return foundName;
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<IEnumerable<IDictionary<string, string>>> ReadCsvAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var allRecords = new List<IDictionary<string, string>>();
            Directory.CreateDirectory(_logFolderPath);
            var logFile = Path.Combine(_logFolderPath, $"readlog_{DateTime.UtcNow:yyyyMMdd}.txt");
            var logLines = new List<string>();
            void Log(string msg)
            {
                var line = $"[AzureBlobCsvFileReaderService][{DateTime.UtcNow:u}] {msg}";
                Console.WriteLine(line);
                logLines.Add(line);
            }
            try
            {
                Log($"CONFIG: ConnectionString={_connectionString}, ContainerName={_containerName}, FilePattern={_filePattern}, LogFolder={_logFolderPath}");
                Log($"TRY CONNECT BlobServiceClient...");
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                Log($"SEARCHING for blob matching: {filePath}");

                //ดึงรายชื่อไฟล์ "ทั้งหมด" ที่ตรงกับ Pattern มา
                var foundBlobNames = await FindBlobFileAsync(containerClient, filePath, cancellationToken);

                if (foundBlobNames == null || !foundBlobNames.Any())
                {
                    Log($"FAIL: No blob found matching pattern: {filePath}");
                    await File.AppendAllLinesAsync(logFile, logLines, cancellationToken);
                    return allRecords;
                }
                Log($"SUCCESS: Found {foundBlobNames.Count} files matching the pattern.");

                // 2. วนลูปอ่านข้อมูลทีละไฟล์
                foreach (var blobName in foundBlobNames)
                {
                    Log($"TRY GET BlobClient for blob: {blobName}");
                    var blobClient = containerClient.GetBlobClient(blobName);

                    Log($"CHECK blob.ExistsAsync for {blobName}...");
                    if (!await blobClient.ExistsAsync(cancellationToken))
                    {
                        Log($"WARNING: Blob not found: {blobName} (Skipping)");
                        continue; // ข้ามไปอ่านไฟล์ถัดไป
                    }

                    Log($"Blob {blobName} found! Opening stream...");
                    using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
                    using var reader = new StreamReader(stream);
                    using var csv = new CsvReader(reader, csvConfig);

                    Log($"Reading CSV header for {blobName}...");
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    var headers = csv.HeaderRecord;
                    if (headers == null)
                    {
                        Log($"WARNING: No header in CSV {blobName} (Skipping)");
                        continue; // ข้ามไปอ่านไฟล์ถัดไป
                    }

                    Log($"Reading CSV records for {blobName}...");
                    int recordCount = 0;
                    while (await csv.ReadAsync())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var dict = new Dictionary<string, string>();
                        foreach (var header in headers)
                        {
                            dict[header] = csv.GetField(header);
                        }
                        allRecords.Add(dict); // นำข้อมูลมารวมกัน
                        recordCount++;
                    }
                    Log($"SUCCESS: Read {recordCount} records from blob: {blobName}");
                }

                Log($"FINAL SUCCESS: Total read {allRecords.Count} records from {foundBlobNames.Count} blobs.");
                await File.AppendAllLinesAsync(logFile, logLines, cancellationToken);

                return allRecords;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                await File.AppendAllLinesAsync(logFile, logLines, cancellationToken);
                return allRecords;
            }
        }
    }
}
