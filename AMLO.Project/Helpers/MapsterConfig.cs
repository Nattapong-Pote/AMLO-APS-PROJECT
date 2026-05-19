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
                .Map(dest => dest.Data, src => src.RawData)
                .Ignore(dest => dest.Id);

            TypeAdapterConfig<AmloDbEntity, AmloDto>.NewConfig()
                .Map(dest => dest.RawData, src => src.Data);


            TypeAdapterConfig.GlobalSettings.ForType<RecordId, RecordId>().MapWith(src => src);

            TypeAdapterConfig<AmloVersionRecord, AmloVersionRecordDto>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.GetId());
        }
    }
    public static class RecordIdExtensions
    {
        public static string? GetId(this RecordId id)
        {
            return id?.DeserializeId<string>();
        }
    }
}
