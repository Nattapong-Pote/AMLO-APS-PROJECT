using AMLO.Project.Helpers;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using AMLO.Project.Services.SurrealDbProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net;

namespace AMLO.Project.Extensions
{
    public static class AmloServiceCollectionExtensions
    {
        public static IServiceCollection AddAmloProject(
            this IServiceCollection services, 
            string dbUrl, 
            string user, 
            string pass, 
            string ns, 
            string db)
        {
            MapsterConfig.RegisterMappings();

            // เชื่อมต่อระบบกับฐานข้อมูล SurrealDB ผ่านมาตรฐาน Connection String
            var connectionString = $"Server={dbUrl};Namespace={ns};Database={db};Username={user};Password={pass}";
            services.AddSurreal(connectionString);

            // ลงทะเบียนหน่วยประมวลผลพื้นฐานของฐานข้อมูล SurrealDB
            services.AddScoped<SurrealDbProviderFactoryBase, SurrealDbProviderFactory>();
            services.AddScoped<SurrealDbProviderFactory>();
            services.AddScoped(typeof(IDbProvider<,>), typeof(DbProvider<,>));

            // ลงทะเบียนตัวจัดการเตรียมฐานข้อมูล
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            // ลงทะเบียนคลาสจัดการข้อมูลในระบบส่วนที่ 2
            services.AddScoped<IProcessDataServiceDAC, ProcessDataServiceDAC>();
            services.AddScoped<IProcessedFileTrackerDAC, ProcessedFileTrackerDAC>();

            // ลงทะเบียนตัวอ่านไฟล์ข้อมูลแยกตามรูปแบบของ Configuration
            services.AddScoped<ICsvFileReaderServiceDAC>(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                var type = config["CsvFileReader:Type"] ?? "Local";
                if (type.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
                    return new AzureBlobCsvFileReaderService(config);
                return new LocalCsvFileReaderService();
            });

            return services;
        }
    }
}
