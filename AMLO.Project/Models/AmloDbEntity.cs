using SurrealDb.Net.Models;

namespace AMLO.Project.Models
{
    public class AmloDbEntity
    {
        public required string TypeName { get; set; }
        public required string Version { get; set; }
        public required Dictionary<string, string> Data { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ArchivedAt { get; set; }
        public bool IsArchived { get; set; }
    }
}
