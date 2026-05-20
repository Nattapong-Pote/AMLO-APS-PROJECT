using AMLO.Project.Helpers;
using AMLO.Project.Models;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using AMLO.Project.Services.SurrealDbProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AMLO.Project.Extensions
{
    public static class AmloServiceCollectionExtensions
    {
        public static IServiceCollection AddAmloProject(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            MapsterConfig.RegisterMappings();

            // เชื่อมต่อระบบกับฐานข้อมูล SurrealDB ผ่านมาตรฐาน Connection String
            services.Configure<SurrealDbConnectionOptions>(
                configuration.GetSection(SurrealDbConnectionOptions.SurrealDb));

            //ดึงค่า Connection String
            var surrealDbConfig = configuration.GetSection(SurrealDbConnectionOptions.SurrealDb).Get<SurrealDbConnectionOptions>();

            if (surrealDbConfig == null || string.IsNullOrWhiteSpace(surrealDbConfig.ConnectionString))
            {
                throw new InvalidOperationException("SurrealDb connection string is missing in appsettings.");
            }

            //เชื่อมต่อ SurrealDb.Net (ส่งค่า Connection String เข้าไปตรงๆ และตั้งเป็น Scoped)
            services.AddSurreal(surrealDbConfig.ConnectionString, ServiceLifetime.Scoped);

            // ลงทะเบียนหน่วยประมวลผลพื้นฐานของฐานข้อมูล SurrealDB
            services.AddScoped<SurrealDbProviderFactoryBase, SurrealDbProviderFactory>();
            services.AddScoped<SurrealDbProviderFactory>();
            services.AddScoped(typeof(IDbProvider<,>), typeof(DbProvider<,>));

            // ลงทะเบียนตัวจัดการเตรียมฐานข้อมูล
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            // ลงทะเบียนคลาสจัดการข้อมูลในระบบส่วนที่ 2
            services.AddScoped<IProcessDataServiceDAC, ProcessDataServiceDAC>();
            services.AddScoped<IProcessedFileTrackerDAC, ProcessedFileTrackerDAC>();
            services.AddScoped<IAmloSyncVersionDAC, AmloSyncVersionDAC>();
            services.AddScoped<ICsvFileReaderServiceDAC, AzureBlobCsvFileReaderService>();

            services.AddScoped<IAmloSyncService, AmloSyncService>();
            services.AddScoped<ICsvMergeService, CsvMergeService>();
            services.AddScoped<IUploadToAzureBlobService, UploadToAzureBlobService>();

            services.AddScoped<DataProcessingService>();

            return services;
        }
    }
}
