namespace AMLO.Project.Models
{
    public class AmloDto
    {
        public required string EntityId { get; set; }
        public required string TypeName { get; set; }
        public required string Version { get; set; }
        public Dictionary<string, string> RawData { get; set; } = new Dictionary<string, string>();
    }
}
