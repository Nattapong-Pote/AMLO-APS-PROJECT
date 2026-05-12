using AMLO.Project.Models;
using Mapster;
using SurrealDb.Net.Models;

namespace AMLO.Project.Helpers
{
    public static class MapsterConfig
    {
        public static void RegisterMappings()
        {
            TypeAdapterConfig<AmloDto, AmloDbEntity>.NewConfig()
            .Map(dest => dest.Data, src => src.RawData);
        }
    }
}
