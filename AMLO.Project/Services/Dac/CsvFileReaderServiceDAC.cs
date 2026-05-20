using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AMLO.Project.Services.Dac;

public interface ICsvFileReaderServiceDAC
{
    /// <summary>
    /// Read CSV file from Local or Azure Blob Storage
    /// </summary>
    Task<IEnumerable<IDictionary<string, string>>> GetAll(string filePath, CancellationToken cancellationToken = default);
}

public class AzureBlobCsvFileReaderService : ICsvFileReaderServiceDAC
{
    private readonly string _connectionString;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobCsvFileReaderService> _logger;

    public AzureBlobCsvFileReaderService(
        IConfiguration config,
        ILogger<AzureBlobCsvFileReaderService> logger)
    {
        _connectionString = config["AzureBlobSettings:ConnectionString"]
            ?? throw new ArgumentNullException(nameof(_connectionString), "ไม่พบการตั้งค่า 'AzureBlobSettings:ConnectionString' ใน appsettings.json");

        _containerName = config["AzureBlobSettings:ContainerName"]
            ?? throw new ArgumentNullException(nameof(_containerName), "ไม่พบการตั้งค่า 'AzureBlobSettings:ContainerName' ใน appsettings.json");

        _logger = logger;
    }

    /// <summary>
    /// Find blob files matching a pattern
    /// </summary>
    private async Task<List<string>> FindBlobFileAsync(BlobContainerClient containerClient, string filePathOrPattern, CancellationToken cancellationToken)
    {
        var matchedFiles = new List<string>();
        if (filePathOrPattern.Contains("*") || filePathOrPattern.Contains("?"))
        {
            var regexPattern = "^" + Regex.Escape(filePathOrPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                if (regex.IsMatch(blobItem.Name))
                {
                    matchedFiles.Add(blobItem.Name);
                }
            }
            return matchedFiles;
        }
        else
        {
            matchedFiles.Add(filePathOrPattern);
        }

        return matchedFiles;
    }

    /// <summary>
    /// Get actual blob file names matching pattern
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

    public async Task<IEnumerable<IDictionary<string, string>>> GetAll(string filePath, CancellationToken cancellationToken = default)
    {
        var allRecords = new List<IDictionary<string, string>>();
        
        try
        {
            _logger.LogInformation("TRY CONNECT BlobServiceClient to container: {ContainerName}...", _containerName);
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            _logger.LogInformation("SEARCHING for blob matching: {FilePath}", filePath);

            var foundBlobNames = await FindBlobFileAsync(containerClient, filePath, cancellationToken);

            if (foundBlobNames == null || !foundBlobNames.Any())
            {
                _logger.LogWarning("FAIL: No blob found matching pattern: {FilePath}", filePath);
                return allRecords;
            }
            _logger.LogInformation("SUCCESS: Found {BlobCount} files matching the pattern.", foundBlobNames.Count);

            foreach (var blobName in foundBlobNames)
            {
                _logger.LogInformation("TRY GET BlobClient for blob: {BlobName}", blobName);
                var blobClient = containerClient.GetBlobClient(blobName);

                _logger.LogInformation("CHECK blob.ExistsAsync for {BlobName}", blobName);
                if (!await blobClient.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("WARNING: Blob not found: {BlobName} (Skipping)", blobName);
                    continue;
                }

                _logger.LogInformation("Blob {BlobName} found! Opening stream...", blobName);
                using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, csvConfig);

                _logger.LogInformation("Reading CSV header for {BlobName}...", blobName);
                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;
                if (headers == null)
                {
                    _logger.LogWarning("WARNING: No header in CSV {BlobName} (Skipping)", blobName);
                    continue;
                }

                _logger.LogInformation("Reading CSV records for {BlobName}...", blobName);
                int recordCount = 0;
                while (await csv.ReadAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var dict = new Dictionary<string, string>();
                    foreach (var header in headers)
                    {
                        dict[header] = csv.GetField(header);
                    }
                    allRecords.Add(dict);
                    recordCount++;
                }
                _logger.LogInformation("SUCCESS: Read {RecordCount} records from blob: {BlobName}", recordCount, blobName);
            }

            _logger.LogInformation("FINAL SUCCESS: Total read {RecordCount} records from {BlobCount} blobs.", allRecords.Count, foundBlobNames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR during CSV reading: {Message}", ex.Message);
        }

        return allRecords;
    }
}
