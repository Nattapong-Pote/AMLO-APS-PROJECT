using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Models
{
    public class AmloVersionRecordDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ListName { get; set; }
        public string? VersionNumber { get; set; }
        public string? Status { get; set; }
        public string? VersionDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
