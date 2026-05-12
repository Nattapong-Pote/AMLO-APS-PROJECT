using AMLO.Project.Models;
using AMLO.Project.Services.Dac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AMLO.Project.Services
{
    public class DataProcessingService
    {
        private readonly IProcessDataServiceDAC _dac;
        private readonly ICsvFileReaderServiceDAC _csvReader;
        private readonly IProcessedFileTrackerDAC _fileTracker;

        public DataProcessingService(IProcessDataServiceDAC dac, ICsvFileReaderServiceDAC csvReader, IProcessedFileTrackerDAC fileTracker)
        {
            _dac = dac;
            _csvReader = csvReader;
            _fileTracker = fileTracker;
        }

        public async Task ImportCsvAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var logTime = DateTime.UtcNow;

            try
            {
                // ⭐ Step 1: ค้นหาชื่อไฟล์ที่พบจริง ถ้าใช้ pattern
                List<string> filesToProcess = new List<string>();
                bool isPatternSearch = fileName.Contains("*") || fileName.Contains("?");

                if (isPatternSearch)
                {
                    // ถ้าใช้ pattern ให้ค้นหาชื่อไฟล์จริง
                    try
                    {
                        var csvReader = _csvReader as Services.Dac.AzureBlobCsvFileReaderService;
                        if (csvReader != null)
                        {
                            filesToProcess = await csvReader.GetActualBlobNamesAsync(fileName, cancellationToken);
                            if (!filesToProcess.Any())
                            {
                                Console.WriteLine($"[INFO] No blob files found matching pattern: {fileName}");
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARN] Could not get actual blob name: {ex.Message}");
                    }
                }
                else
                {
                    filesToProcess.Add(fileName);
                }

                //วนลูป "ทีละไฟล์" เพื่อทำงาน
                foreach (var actualFileName in filesToProcess)
                {
                    Console.WriteLine($"\n[INFO] Start processing file: {actualFileName}");

                    // 2.1 เอาชื่อไฟล์จริง (เช่น al-qaida...csv) มาเช็คว่าเคยทำหรือยัง
                    var isAlreadyProcessed = await _fileTracker.IsFileAlreadyProcessedAsync(actualFileName, cancellationToken);
                    if (isAlreadyProcessed)
                    {
                        // ถ้าเคยทำแล้ว ให้ข้าม (Skip) ไปไฟล์ถัดไปทันที
                        var skipMessage = $"[SKIPPED] File already processed: {actualFileName} at {logTime:u}";
                        Console.WriteLine(skipMessage);
                        await _fileTracker.RecordFileSkippedAsync(actualFileName, "Duplicate file - already processed and saved to database", cancellationToken);

                        continue; // ⚡ กลับไปขึ้นต้น Loop ใหม่สำหรับไฟล์ถัดไป
                    }

                    // 2.2 ถ้าเป็นไฟล์ใหม่ (เช่น taliban...csv) ให้ส่งชื่อไฟล์ตรงๆ ไปอ่าน
                    // ซึ่ง Reader จะอ่านแค่ไฟล์นี้ไฟล์เดียว เพราะไม่มีเครื่องหมาย * แล้ว
                    var records = await _csvReader.ReadCsvAsync(actualFileName, cancellationToken);
                    if (records == null || !records.Any())
                    {
                        Console.WriteLine($"[Warning] No records found in: {actualFileName}");
                        await _fileTracker.RecordFileFailedAsync(actualFileName, "No records found in file", cancellationToken);
                        continue;
                    }

                    // 2.3 บันทึกลงฐานข้อมูล
                    var recordCount = 0;
                    foreach (var row in records)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!row.TryGetValue("ENTITY_ID", out var entityId) || string.IsNullOrEmpty(entityId))
                            continue;

                        var dto = new AmloDto { EntityId = entityId };
                        foreach (var kv in row)
                        {
                            if (kv.Key.Equals("ENTITY_ID", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!string.IsNullOrEmpty(kv.Value)) dto.RawData[kv.Key] = kv.Value;
                        }

                        await _dac.UpsertDataAsync(dto, cancellationToken);
                        recordCount++;
                    }

                    // 2.4 บันทึกว่าไฟล์ใหม่นี้ทำสำเร็จแล้ว
                    await _fileTracker.RecordFileProcessedAsync(actualFileName, recordCount, cancellationToken: cancellationToken);
                    Console.WriteLine($"[Success] Import completed: {recordCount} records processed from file: {actualFileName}");
                }

                Console.WriteLine("\n[INFO] All matching files have been processed completely.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] ImportCsvAsync failed for {fileName}: {ex.Message}");
                await _fileTracker.RecordFileFailedAsync(fileName, $"Error: {ex.Message}", cancellationToken);
                throw;
            }
        }
    }
}
