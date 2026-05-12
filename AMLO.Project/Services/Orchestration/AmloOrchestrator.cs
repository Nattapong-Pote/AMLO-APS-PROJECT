using AMLO.Project.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Services.Orchestration
{
    public class AmloOrchestrator
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<AmloOrchestrator> _logger;
        private readonly CsvMergeService _mergeService;

        public AmloOrchestrator(IMemoryCache cache, ILogger<AmloOrchestrator> logger, CsvMergeService mergeService)
        {
            _cache = cache;
            _logger = logger;
            _mergeService = mergeService;
        }

        public async Task ExecuteNightBatchAsync(List<AmloApiConfig> apis, string tempExtractPath, string finalOutputPath)
        {
            var tasks = new List<Task>();

            // สั่งรัน API ทุกตัวพร้อมกันโดยไม่รอคิว (Concurrency)
            foreach (var api in apis)
            {
                tasks.Add(ProcessSingleApiAsync(api, tempExtractPath));
            }

            // รอจนกว่าทุก API จะทำงานเสร็จ
            await Task.WhenAll(tasks);

            _logger.LogInformation("All APIs processed. Starting CSV Merge...");

            // นำไฟล์ทั้งหมดใน tempExtractPath มารวมเป็น single.csv
            _mergeService.MergeExtractedCsvFiles(tempExtractPath, finalOutputPath);

            _logger.LogInformation($"Merge complete. File saved to {finalOutputPath}");

            // TODO: โค้ดสำหรับอัปโหลดไฟล์ single.csv ขึ้น Azure Storage (เงื่อนไขข้อ 5)
        }

        private async Task ProcessSingleApiAsync(AmloApiConfig api, string extractPath)
        {
            try
            {
                // สมมติ: คุณยิง API เพื่อดึง Version ล่าสุดมาได้แล้ว
                string currentVersion = "VERSION_12345";

                string cacheKey = $"AMLO_VER_{api.ServiceName}";

                // ตรวจสอบ In-Memory
                if (_cache.TryGetValue(cacheKey, out string? cachedVersion) && cachedVersion == currentVersion)
                {
                    _logger.LogInformation($"[PASS - SKIPPED] API: {api.ServiceName} | Version: {currentVersion} | Status: Already up to date.");
                    return;
                }

                _logger.LogInformation($"[PROCESS] API: {api.ServiceName} | Version: {currentVersion} | Status: Downloading new data...");

                // TODO: โลจิกยิง API ไปที่ api.DataUrl > ถอดรหัส > แตกไฟล์ CSV ไปไว้ที่ extractPath

                // สมมติว่าทำงานเสร็จเรียบร้อย ให้จำ Version นี้ไว้ใน Cache
                _cache.Set(cacheKey, currentVersion);

                _logger.LogInformation($"[PASS - SUCCESS] API: {api.ServiceName} | Version: {currentVersion} | Status: Data extracted.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FAIL] API: {api.ServiceName} | Error: {ex.Message}");
            }
        }
    }
}
