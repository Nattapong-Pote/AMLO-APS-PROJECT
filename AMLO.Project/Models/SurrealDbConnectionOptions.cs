using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Models
{
    public class SurrealDbConnectionOptions
    {
        public const string SurrealDb = "SurrealDb";
        public string ConnectionString { get; set; } = string.Empty;
    }
}
