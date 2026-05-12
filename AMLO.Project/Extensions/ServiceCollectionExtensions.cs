using AMLO.Project.Helpers;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
            
            // Manual registration with explicit configuration
            // This MUST register ISurrealDbClient as a service
            services.AddScoped<ISurrealDbClient>(provider =>
            {
                // Create client with endpoint
                var client = new SurrealDbClient(dbUrl);
                return client;
            });

            // Register Database Initializer
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            // Register ProcessDataService
            services.AddScoped<IProcessDataServiceDAC, ProcessDataServiceDAC>();

            // Register ProcessedFileTracker
            services.AddScoped<IProcessedFileTrackerDAC, ProcessedFileTrackerDAC>();

            // Register CsvFileReaderService (เลือก implementation ตาม config)
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
