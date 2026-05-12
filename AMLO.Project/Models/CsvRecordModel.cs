namespace AMLO.Project.Models
{
    public class CsvRecordModel
    {
        public required string EntityId { get; set; }
        public Dictionary<string, string> RawData { get; set; } = new Dictionary<string, string>();
    }
}
