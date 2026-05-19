using System;
using System.Collections.Generic;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace AMLO.Project.Services
{
    public interface IUploadToAzureBlobService
    {
        Task UploadToAzureBlobAsync(string localFilePath);
    }
    public class UploadToAzureBlobService : IUploadToAzureBlobService
    {
        private readonly string connectionString;
        private readonly string containerName;
        public UploadToAzureBlobService(IConfiguration configuration)
        {
            // 1. กำหนดค่า Connection String และชื่อ Container ของคุณ
            this.connectionString = configuration["AzureBlobSettings:ConnectionString"] ?? throw new ArgumentNullException("ConnectionString");
            this.containerName = configuration["AzureBlobSettings:ContainerName"] ?? throw new ArgumentNullException("ContainerName");
        }

        public async Task UploadToAzureBlobAsync(string localFilePath)
        {
            // ดึงเฉพาะชื่อไฟล์ออกมาจาก Path (เช่น freeze-05_..._combine.csv)
            string fileName = Path.GetFileName(localFilePath);

            try
            {
                // 2. สร้าง Service Client เพื่อเชื่อมต่อกับ Storage Account
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

                // 3. เชื่อมต่อไปยัง Container (ถ้าไม่มีให้สร้างใหม่)
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                // 4. ระบุชื่อ Blob (ชื่อไฟล์) ที่ต้องการสร้างบน Azure
                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                // 5. เปิดไฟล์และอัปโหลด
                using FileStream uploadFileStream = File.OpenRead(localFilePath);

                // uploadFileStream: ไฟล์ที่จะอัปโหลด
                // overwrite: true (หมายถึงถ้ามีไฟล์ชื่อนี้อยู่แล้วบน Azure ให้ทับไปเลย)
                await blobClient.UploadAsync(uploadFileStream, overwrite: true);

                uploadFileStream.Close();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload to Azure Blob Storage: {ex.Message}");
            }
        }

    }
}
