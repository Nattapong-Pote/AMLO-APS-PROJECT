using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AMLO.Project.Services
{
    public interface ICsvMergeService
    {
        void MergeExtractedCsvFiles(string extractedFolderPath, string fileNameCombine, string versionCombine);
    }
    public class CsvMergeService : ICsvMergeService
    {
        public void MergeExtractedCsvFiles(string extractedFolderPath, string amloName, string amloVersion)
        {
            string fileNameCombine = $"{amloName}_{amloVersion}_{DateTime.Now:yyyyMMddHH}_combine.csv";
            string outputSingleCsvPath = Path.Combine(extractedFolderPath, fileNameCombine);
            var mergedData = new Dictionary<string, Dictionary<string, string>>();
            List<string> allColumns = ["ENTITY_ID"]; // C# 12+ Collection Expression

            string[] files = Directory.GetFiles(extractedFolderPath, "*.csv");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                BadDataFound = null,
                MissingFieldFound = null
            };

            // วนลูปอ่านทีละไฟล์
            foreach (var file in files)
            {
                using var reader = new StreamReader(file);
                using var csv = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;

                if (headers == null || !headers.Contains("ENTITY_ID", StringComparer.OrdinalIgnoreCase))
                    continue;

                // อัปเดตรายชื่อคอลัมน์ทั้งหมด
                foreach (var header in headers)
                {
                    if (!allColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                        allColumns.Add(header);
                }

                // นำข้อมูลมารวมกันตาม ENTITY_ID
                while (csv.Read())
                {
                    var entityId = csv.GetField("ENTITY_ID");
                    if (string.IsNullOrEmpty(entityId)) continue;

                    if (!mergedData.ContainsKey(entityId))
                    {
                        mergedData[entityId] = new Dictionary<string, string> { { "ENTITY_ID", entityId } };
                    }

                    foreach (var header in headers)
                    {
                        if (header.Equals("ENTITY_ID", StringComparison.OrdinalIgnoreCase)) continue;

                        var value = csv.GetField(header);
                        if (!string.IsNullOrEmpty(value))
                        {
                            if (mergedData[entityId].ContainsKey(header))
                            {
                                if (!mergedData[entityId][header].Contains(value))
                                    mergedData[entityId][header] += $" | {value}";
                            }
                            else
                            {
                                mergedData[entityId][header] = value;
                            }
                        }
                    }
                }
            }

            // เขียนออกเป็นไฟล์ single.csv
            using var writer = new StreamWriter(outputSingleCsvPath);
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            foreach (var col in allColumns) csvWriter.WriteField(col);
            csvWriter.NextRecord();

            foreach (var entityId in mergedData.Keys)
            {
                var rowData = mergedData[entityId];
                foreach (var col in allColumns)
                {
                    rowData.TryGetValue(col, out var val);
                    csvWriter.WriteField(val ?? "");
                }
                csvWriter.NextRecord();
            }
        }
    }
}
