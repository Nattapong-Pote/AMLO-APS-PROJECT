namespace AMLO.Project.Models
{
    public class AmloApiConfig
    {
        public required string ServiceName { get; init; }
        public required string VersionUrl { get; init; }
        public required string DataUrl { get; init; }
    }
}
