using AMLO.Project.Helpers;
using AMLO.Project.Services;
using AMLO.Project.Services.Dac;
using AMLO.Project.Services.SurrealDbProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net;

namespace AMLO.Project.Extensions
{
    /// <summary>
    /// Container for SurrealDB authentication context
    /// </summary>
    public class SurrealDbAuthContext
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Namespace { get; set; }
        public string Database { get; set; }
    }

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

            // Store auth context in DI for later use
            var authContext = new SurrealDbAuthContext
            {
                Username = user,
                Password = pass,
                Namespace = ns,
                Database = db
            };

            services.AddSingleton(authContext);

            // Register SurrealDbClient as SINGLETON
            // This ensures there is only ONE authenticated instance used throughout the application
            // The single instance will be authenticated once and reused by all services
            services.AddSingleton<ISurrealDbClient>(provider =>
            {
                var client = new SurrealDbClient(dbUrl);
                return client;
            });

            services.AddSingleton<SurrealDbProviderFactoryBase, SurrealDbProviderFactory>();
            services.AddSingleton<SurrealDbProviderFactory>();

            services.AddScoped(typeof(IDbProvider<,>), typeof(DbProvider<,>));

            // Register Database Initializer (will handle auth)
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            // Register ProcessDataService
            services.AddScoped<IProcessDataServiceDAC, ProcessDataServiceDAC>();

            // Register ProcessedFileTracker
            services.AddScoped<IProcessedFileTrackerDAC, ProcessedFileTrackerDAC>();

            // Register CsvFileReaderService
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
