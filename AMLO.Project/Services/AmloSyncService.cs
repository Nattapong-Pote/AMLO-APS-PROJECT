using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AMLO.Project.Models;
using AMLO.Project.Services.Dac;
using Flurl.Http;
using Mapster;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PgpCore;

namespace AMLO.Project.Services
{
    public interface IAmloSyncService
    {
        Task<string> SyncAllParallelAsync();
        Task<List<AmloVersionRecordDto>> GetAllVersionFromDbAsync();
    }
    public class AmloSyncService : IAmloSyncService, IDisposable
    {
        private readonly IConfiguration config;
        private readonly IAmloSyncVersionDAC amloVersionRecordDAC;
        private readonly IMemoryCache memoryCache;
        private readonly ICsvMergeService csvMergeService;
        private readonly IUploadToAzureBlobService uploadToAzureBlobService;

        // Constants for paths and configuration keys
        private const int CACHE_TTL_HOURS = 12;
        private const string CACHE_KEY_PREFIX = "amlo_list_";
        private const string DATA_FOLDER = "Data";
        private const string AMLO_FILES_FOLDER = "AmloFiles";

        // ตัวแปรสำหรับแชร์ FlurlClient และตัวควบคุมการเข้าถึง
        private IFlurlClient? flurlClient;
        private readonly SemaphoreSlim clientLock = new SemaphoreSlim(1, 1);
        private List<AmloEndpoint> amloConfig;

        public AmloSyncService(IAmloSyncVersionDAC amloVersionRecordDAC, IConfiguration config, IMemoryCache memoryCache, ICsvMergeService csvMergeService, IUploadToAzureBlobService uploadToAzureBlobService)
        {
            amloConfig = config.GetSection(nameof(AmloConfig)).Get<AmloConfig>().Endpoints;
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            this.amloVersionRecordDAC = amloVersionRecordDAC ?? throw new ArgumentNullException(nameof(amloVersionRecordDAC));
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.csvMergeService = csvMergeService ?? throw new ArgumentNullException(nameof(csvMergeService));
            this.uploadToAzureBlobService = uploadToAzureBlobService;

            // Validate required configuration values
            ValidateConfiguration();
        }

        // ฟังก์ชันสำหรับตรวจสอบความครบถ้วนของการตั้งค่าใน IConfiguration
        private void ValidateConfiguration()
        {
            var requiredSettings = new[]
            {
                //"Amlo:BaseUrl",
                "Amlo:CertFileName",
                "Amlo:KeyFileName",
                "Amlo:Keypassword",
                "Amlo:CaPassword",
                "Amlo:Username",
                "Amlo:Password",
                "Amlo:XApiKey"
            };
            foreach (var setting in requiredSettings)
            {
                if (string.IsNullOrEmpty(config[setting]))
                {
                    throw new InvalidOperationException($"Required configuration setting '{setting}' is missing or empty.");
                }
            }

            // Validate Endpoints instead of ListNames
            if (amloConfig == null || !amloConfig.Any())
            {
                throw new InvalidOperationException("Required configuration setting 'AmloConfig:Endpoints' is missing or empty.");
            }
        }

        // Method สร้าง IFlurlClient แค่ครั้งเดียว (Thread-safe)
        private async Task<IFlurlClient> GetOrCreateFlurlClientAsync()
        {
            if (flurlClient != null) return flurlClient;

            await clientLock.WaitAsync();
            try
            {
                if (flurlClient != null) return flurlClient;

                var handler = new HttpClientHandler();

                var certPass = config["Amlo:CaPassword"];
                string certFileName = config["Amlo:CertFileName"];
                string projectRoot = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
                string certPath = Path.Combine(projectRoot, DATA_FOLDER, certFileName);

                if (File.Exists(certPath))
                {
                    var certificate = new X509Certificate2(certPath, certPass);
                    handler.ClientCertificates.Add(certificate);
                }

                // สร้าง HttpClient พื้นฐานที่แนบ Cert แล้ว และหุ้มด้วย FlurlClient พร้อมตั้งค่า Headers/Auth
                var httpClient = new HttpClient(handler);
                flurlClient = new FlurlClient(httpClient)
                    .WithHeader("X-API-Key", config["Amlo:XApiKey"])
                    .WithBasicAuth(config["Amlo:Username"], config["Amlo:Password"]);

                return flurlClient;
            }
            finally
            {
                clientLock.Release();
            }
        }

        // ฟังก์ชันสำหรับดึงข้อมูล Version ทั้งหมดจาก DB
        public async Task<List<AmloVersionRecordDto>> GetAllVersionFromDbAsync()
        {
            return await amloVersionRecordDAC.GetAllAsync();
        }

        // ฟังก์ชันหลักสำหรับ Sync ข้อมูลทั้งหมดแบบขนาน (Parallel)
        public async Task<string> SyncAllParallelAsync()
        {
            try
            {
                // Step 1: ดึง amloConfig เฉพาะ "Name" มาใส่เป็น Array
                var listNames = amloConfig.Select(e => e.Name).ToArray();
                var endpointMap = amloConfig.ToDictionary(e => e.Name, e => e);

                // Step 2: ดึงข้อมูล Version จาก AMLO API แบบ parallel
                var versionsFromAMLO = await GetVersionAllTypeAsync(listNames, endpointMap);

                // Step 3: Map version data
                var versionMap = new Dictionary<string, string>();
                foreach (var version in versionsFromAMLO)
                {
                    if (version.Name != null && version.VersionNumber != null)
                    {
                        versionMap[version.Name] = version.VersionNumber;
                    }
                }

                // Step 4: เปรียบเทียบ Version และเก็บ list ที่อัปเดต
                var comparisonTasks = listNames.Select(async name =>
                {
                    if (!versionMap.ContainsKey(name))
                    {
                        throw new KeyNotFoundException($"Version data for '{name}' not found from AMLO API");
                    }

                    var newVersion = versionMap[name];
                    var endpoint = endpointMap[name];
                    var codeName = endpoint.ListName;

                    var isUpdated = await CompareVersionAsync(name, newVersion, codeName);
                    return new { Name = name, IsUpdated = isUpdated };
                });

                var comparisonResults = await Task.WhenAll(comparisonTasks);
                var updatedLists = comparisonResults.Where(x => x.IsUpdated).Select(x => x.Name).ToList();
                var cachedLists = comparisonResults.Where(x => !x.IsUpdated).Select(x => x.Name).ToList();

                // Download files ที่จำเป็น
                var downloadResults = new Dictionary<string, bool>();
                if (updatedLists.Count > 0)
                {
                    downloadResults = await DownloadAndStoreFilesAsync(updatedLists, versionsFromAMLO, endpointMap);
                }

                // Update database + cache ตามผลการ download
                var successfulDownloads = new List<string>();
                var failedDownloads = new List<string>();

                foreach (var downloadResult in downloadResults)
                {
                    var listName = downloadResult.Key;
                    var isSuccess = downloadResult.Value;
                    var endpoint = endpointMap[listName];
                    var codeName = endpoint.ListName;
                    var newVersion = versionMap[listName];
                    var newVersionDate = versionsFromAMLO.FirstOrDefault(v => v.Name == listName)?.VersionDate ?? "";

                    if (isSuccess)
                    {
                        await UpdateVersionInDatabaseAsync(listName, codeName, newVersion, newVersionDate);
                        successfulDownloads.Add(listName);
                    }
                    else
                    {
                        await FailVersionInDatabaseAsync(listName, codeName, newVersion, newVersionDate);
                        failedDownloads.Add(listName);
                    }
                }

                // Step 5: สร้าง result
                var syncResult = new
                {
                    syncTime = DateTime.UtcNow.ToString("O"),
                    totalLists = listNames.Length,
                    checkedLists = listNames.Length,
                    requiresUpdateLists = updatedLists,
                    skipDueToNoCacheFailedLists = cachedLists,
                    successfulDownloads = successfulDownloads,
                    failedDownloads = failedDownloads,
                    message = $"Sync completed. {successfulDownloads.Count} successful, {failedDownloads.Count} failed."
                };

                return JsonSerializer.Serialize(syncResult);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        public async Task<Dictionary<string, bool>> DownloadAndStoreFilesAsync(List<string> names, List<AmloVersionRecordDto> versionsFromAMLO, Dictionary<string, AmloEndpoint> endpointMap)
        {
            string projectRoot = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;

            var endpoints = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var endpoint = endpointMap[name];
                endpoints.Add(name, endpoint.DataEndpoint);
            }

            var amloFilesPath = Path.Combine(projectRoot, DATA_FOLDER, AMLO_FILES_FOLDER);
            Directory.CreateDirectory(amloFilesPath);

            using var semaphore = new SemaphoreSlim(2, 2);
            var downloadResults = new Dictionary<string, bool>(); // Track success/fail

            var tasks = endpoints.Where(e => names.Contains(e.Key)).Select(async e =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var fileContent = await SendRequestAsync(e.Value);

                    if (string.IsNullOrEmpty(fileContent))
                    {
                        downloadResults[e.Key] = false;
                        return $"No content for {e.Key}";
                    }

                    APSData? apsData = JsonSerializer.Deserialize<APSData>(fileContent);

                    if (apsData == null)
                    {
                        downloadResults[e.Key] = false;
                        return $"Failed to deserialize JSON for {e.Key}";
                    }

                    if (string.IsNullOrEmpty(apsData.Result))
                    {
                        downloadResults[e.Key] = false;
                        return $"APSData.Result is empty for {e.Key}";
                    }

                    string fileNameTxt = e.Key + ".zip.pgp";

                    byte[] data = Convert.FromBase64String(apsData.Result);
                    string decodedString = Encoding.UTF8.GetString(data);

                    if (string.IsNullOrEmpty(decodedString))
                    {
                        downloadResults[e.Key] = false;
                        return $"Decoded string is empty for {e.Key}";
                    }

                    string keyFileName = config["Amlo:KeyFileName"];
                    string keyPassword = config["Amlo:Keypassword"];
                    string keyPath = Path.Combine(projectRoot, DATA_FOLDER, keyFileName);

                    if (File.Exists(keyPath))
                    {
                        try
                        {
                            string folderName = fileNameTxt.Replace(".zip", "", StringComparison.InvariantCultureIgnoreCase).Replace(".pgp", "", StringComparison.InvariantCultureIgnoreCase);
                            string folderPath = Path.Combine(projectRoot, DATA_FOLDER, AMLO_FILES_FOLDER, folderName);
                            string encryptedFilePath = Path.Combine(amloFilesPath, fileNameTxt);

                            await File.WriteAllTextAsync(encryptedFilePath, decodedString);

                            FileInfo privateKey = new FileInfo(keyPath);
                            EncryptionKeys encryptionKeys = new EncryptionKeys(privateKey, keyPassword);

                            using (PGP pgp = new PGP(encryptionKeys))
                            {
                                FileInfo encryptPath = new FileInfo(encryptedFilePath);
                                FileInfo decryptPath = new FileInfo(Path.Combine(amloFilesPath, fileNameTxt.Replace(".pgp", "", StringComparison.InvariantCultureIgnoreCase)));

                                await pgp.DecryptFileAsync(encryptPath, decryptPath);

                                if (!Directory.Exists(folderPath))
                                {
                                    Directory.CreateDirectory(folderPath);
                                }
                                ZipFile.ExtractToDirectory(decryptPath.FullName, folderPath, overwriteFiles: true);

                                //combine csv files in folder to one csv file
                                var versionRecord = versionsFromAMLO.FirstOrDefault(v => v.Name == e.Key);
                                if (versionRecord != null)
                                {
                                    csvMergeService.MergeExtractedCsvFiles(folderPath, versionRecord.Name, versionRecord.VersionNumber);

                                    var combineFiles = Directory.GetFiles(folderPath, "*_combine.csv");
                                    foreach (var file in combineFiles)
                                    {
                                        // เรียกใช้ฟังก์ชันอัปโหลด Azure Blob Storage
                                        await uploadToAzureBlobService.UploadToAzureBlobAsync(file);
                                    }

                                    // Clean up individual csv files หลังจากรวมเสร็จแล้ว (เหลือแค่ไฟล์ *_combine.csv)
                                    CleanUpFiles(folderPath, ".csv", "_combine.csv");

                                }
                            }

                            downloadResults[e.Key] = true;
                            return $"Downloaded and stored {e.Key}";
                        }
                        catch (Exception ex)
                        {
                            downloadResults[e.Key] = false;
                            return $"Decryption failed for {e.Key}";
                        }
                    }
                    else
                    {
                        downloadResults[e.Key] = false;
                        return $"Key file not found for {e.Key}";
                    }
                }
                catch (Exception ex)
                {
                    downloadResults[e.Key] = false;
                    return $"Error downloading {e.Key}: {ex.Message}";
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Clean up .pgp files and .zip files
            CleanUpFiles(amloFilesPath, ".pgp", null);
            CleanUpFiles(amloFilesPath, ".zip", null);

            return downloadResults;
        }

        // ฟังก์ชันช่วยลบไฟล์ที่ไม่ต้องการในโฟลเดอร์ (เช่น ลบ .csv ที่แยกแต่เก็บ .csv ที่รวมแล้ว)
        private void CleanUpFiles(string folderPath, string fileExtension, string? endsWith)
        {
            try
            {
                var files = Directory.GetFiles(folderPath, $"*{fileExtension}");
                foreach (var file in files)
                {
                    if (!string.IsNullOrEmpty(endsWith))
                    {
                        if (!file.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(file);
                        }
                    }
                    else
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to clean up {fileExtension} files: {ex.Message}");
            }
        }

        // ดึง Version จาก in-memory cache
        public AmloVersionRecordDto? GetCachedVersion(string name)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{name}";
            memoryCache.TryGetValue(cacheKey, out AmloVersionRecordDto? cachedVersion);
            return cachedVersion;
        }

        // เก็บ Version ใน in-memory cache (TTL = 12 hours)
        public void SetCache(string name, AmloVersionRecordDto record)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{name}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CACHE_TTL_HOURS)
            };
            memoryCache.Set(cacheKey, record, cacheOptions);
        }

        // เปรียบเทียบ Version + Status: Cache → DB → ใหม่
        // Return: true = ต้อง download, false = skip download
        public async Task<bool> CompareVersionAsync(string name, string newVersionFromAMLO, string codeName)
        {
            // Step 1: ตรวจ In-Memory Cache
            var cachedVersion = GetCachedVersion(name);
            if (cachedVersion != null)
            {
                // ถ้า version เดียวกัน AND status = success → Skip (false)
                if (cachedVersion.VersionNumber == newVersionFromAMLO && cachedVersion.Status == "success")
                {
                    return false;
                }
                // ถ้า version เดียวกัน แต่ status != success → Retry (true)
                if (cachedVersion.VersionNumber == newVersionFromAMLO && cachedVersion.Status != "success")
                {
                    return true;
                }
                // ถ้า version ต่างกัน → Download (true)
                return true;
            }

            // Step 2: Cache miss → ดึงจาก DB
            var dbVersion = await amloVersionRecordDAC.GetByNameAsync(name);
            if (dbVersion != null)
            {
                // สร้าง cache record
                var recordToCache = new AmloVersionRecordDto
                {
                    Name = name,
                    ListName = codeName,
                    VersionNumber = newVersionFromAMLO,
                    Status = dbVersion.Status, //pending
                    VersionDate = dbVersion.VersionDate,
                };
                SetCache(name, recordToCache);

                // ถ้า version เดียวกัน AND status = success → Skip (false)
                if (dbVersion.VersionNumber == newVersionFromAMLO && dbVersion.Status == "success")
                {
                    return false;
                }
                // ถ้า version เดียวกัน แต่ status != success → Retry (true)
                if (dbVersion.VersionNumber == newVersionFromAMLO && dbVersion.Status != "success")
                {
                    return true;
                }
                // ถ้า version ต่างกัน → Download (true)
                return true;
            }

            // Step 3: ไม่พบใน Cache และ DB → ถือว่าเป็น version ใหม่ → Download (true)
            var newRecord = new AmloVersionRecordDto
            {
                Name = name,
                ListName = codeName,
                VersionNumber = newVersionFromAMLO,
                Status = "pending",
                VersionDate = "",
            };
            SetCache(name, newRecord);
            return true;
        }

        // Update version ที่ download สำเร็จเข้า DB: version=new, status=success
        private async Task UpdateVersionInDatabaseAsync(string name, string codeName, string newVersion, string newVersionDate)
        {
            try
            {
                var existingRecord = await amloVersionRecordDAC.GetByNameAsync(name);

                if (existingRecord != null && !string.IsNullOrEmpty(existingRecord.Id))
                {
                    // Update existing record
                    var updateData = new Dictionary<string, object?>();
                    updateData.Add("VersionNumber", newVersion);
                    updateData.Add("ListName", codeName);
                    updateData.Add("Status", "success");
                    updateData.Add("VersionDate", newVersionDate);
                    updateData.Add("UpdatedDate", DateTime.UtcNow);

                    await amloVersionRecordDAC.UpdateAmloVersionRecordAsync(existingRecord.Id, updateData);

                    existingRecord = updateData.Adapt(existingRecord);
                    SetCache(name, existingRecord);
                }
                else
                {
                    // Insert new record
                    var newRecord = new AmloVersionRecordDto
                    {
                        Name = name,
                        ListName = codeName,
                        VersionNumber = newVersion,
                        Status = "success",
                        VersionDate = newVersionDate,
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow
                    };

                    await amloVersionRecordDAC.CreateAmloVersionRecordAsync(newRecord);

                    SetCache(name, newRecord);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update database for {name} (success status): {ex.Message}");
            }
        }

        // Update version ที่ download ล้มเหลวเข้า DB: version=new, status=failed และ update cache
        private async Task FailVersionInDatabaseAsync(string name, string codeName, string newVersion, string newVersionDate)
        {
            try
            {
                var existingRecord = await amloVersionRecordDAC.GetByNameAsync(name);

                if (existingRecord != null && !string.IsNullOrEmpty(existingRecord.Id))
                {
                    // Update existing record
                    var updateData = new Dictionary<string, object?>();
                    updateData.Add("VersionNumber", newVersion);
                    updateData.Add("ListName", codeName);
                    updateData.Add("Status", "failed");
                    updateData.Add("VersionDate", newVersionDate);
                    updateData.Add("UpdatedDate", DateTime.UtcNow.ToString("O"));

                    await amloVersionRecordDAC.UpdateAmloVersionRecordAsync(existingRecord.Id, updateData);

                    existingRecord = updateData.Adapt(existingRecord);
                    SetCache(name, existingRecord);
                }
                else
                {
                    // Insert new record
                    var newRecord = new AmloVersionRecordDto
                    {
                        Name = name,
                        ListName = codeName,
                        VersionNumber = newVersion,
                        Status = "failed",
                        VersionDate = newVersionDate,
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow
                    };

                    await amloVersionRecordDAC.CreateAmloVersionRecordAsync(newRecord);

                    SetCache(name, newRecord);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update database for {name} (failed status): {ex.Message}");
            }
        }

        // ฟังก์ชันสำหรับดึงข้อมูล Version จาก AMLO API แบบขนาน (Parallel) โดยใช้ listNames ที่ส่งเข้ามา
        public async Task<List<AmloVersionRecordDto>> GetVersionAllTypeAsync(string[] listNames, Dictionary<string, AmloEndpoint> endpointMap)
        {
            var requestEndpoints = new Dictionary<string, string>();
            foreach (var name in listNames)
            {
                if (endpointMap.TryGetValue(name, out var endpoint))
                {
                    requestEndpoints[name] = endpoint.VersionEndpoint;
                }
            }

            // จำกัดการขอ Version พร้อมกันที่ 3 คิว
            using var semaphore = new SemaphoreSlim(3, 3);

            var tasks = requestEndpoints.Select(async endpointEntry =>
            {
                var item = new AmloVersionRecordDto { Name = endpointEntry.Key };

                await semaphore.WaitAsync();
                try
                {
                    var result = await SendRequestAsync(endpointEntry.Value);

                    if (string.IsNullOrEmpty(result))
                    {
                        item.Status = "failed";
                        return item;
                    }

                    using var doc = JsonDocument.Parse(result);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var data = root[0];
                        item.ListName = data.GetProperty("LIST_NAME").GetString();
                        item.VersionNumber = data.GetProperty("VERSION_NUMBER").GetString();
                        item.Status = "success";
                        item.VersionDate = data.GetProperty("CREATE_DATE").GetString();
                    }
                    else
                    {
                        item.Status = "failed";
                    }
                }
                catch (Exception ex)
                {
                    item.Status = "error";
                }
                finally
                {
                    semaphore.Release();
                }
                return item;
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }


        // ฟังก์ชันสำหรับส่ง HTTP Request ด้วย FlurlClient ที่สร้างไว้แล้ว พร้อมจัดการ Rate Limit (429) และ Network Error อื่นๆ
        private async Task<string> SendRequestAsync(string url)
        {
            const int maxRetries = 5;
            const int initialDelayMs = 1000;

            // ดึง FlurlClient ที่สร้างไว้แล้ว (รวม Auth, Cert, Headers ครบ)
            var client = await GetOrCreateFlurlClientAsync();

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // ใช้คำสั่งของ Flurl: ส่ง Post แบบ Empty JSON และรอรับ Response เป็นข้อความ
                    return await client.Request(url)
                        .PostJsonAsync(new { })
                        .ReceiveString();
                }
                catch (FlurlHttpException ex) when (ex.StatusCode == 429 && attempt < maxRetries - 1)
                {
                    // จัดการ Rate Limit (429) แบบทวีคูณ (Exponential Backoff)
                    int delayMs = initialDelayMs * (int)Math.Pow(2, attempt);
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    // จัดการ Network Error อื่นๆ (เช่น Timeout, Connection Drop)
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(initialDelayMs);
                        continue;
                    }
                    throw; // โยน Error ออกไปเมื่อครบจำนวน Max Retries แล้ว
                }
            }

            throw new Exception($"Failed to get response from {url} after {maxRetries} attempts");
        }

        // Implement IDisposable เพื่อคืนทรัพยากร
        public void Dispose()
        {
            flurlClient?.Dispose();
            clientLock?.Dispose();
        }

    }
}
