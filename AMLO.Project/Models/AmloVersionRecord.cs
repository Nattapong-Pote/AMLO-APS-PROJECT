using System;
using System.Collections.Generic;
using System.Text;
using SurrealDb.Net.Models;

namespace AMLO.Project.Models
{
    public class AmloVersionRecord : Record
    {
        public string? Name { get; set; }
        public string? ListName { get; set; }
        public string? VersionNumber { get; set; }
        public string? Status { get; set; }
        public string? VersionDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
