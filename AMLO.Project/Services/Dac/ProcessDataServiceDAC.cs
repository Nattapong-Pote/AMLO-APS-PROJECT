using AMLO.Project.Models;
using Mapster;
using SurrealDb.Net;

namespace AMLO.Project.Services.Dac
{
    public interface IProcessDataServiceDAC
    {
        Task UpsertDataAsync(AmloDto dtoData, CancellationToken cancellationToken = default);
    }

    public class ProcessDataServiceDAC: IProcessDataServiceDAC
    {
        private readonly ISurrealDbClient _dbClient;

        public ProcessDataServiceDAC(ISurrealDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task UpsertDataAsync(AmloDto dtoData, CancellationToken cancellationToken = default)
        {
            var entity = dtoData.Adapt<AmloDbEntity>();
            
            // ✅ Add CancellationToken parameter
            await _dbClient.Upsert("amlo_master", entity, cancellationToken);
        }
    }
}
