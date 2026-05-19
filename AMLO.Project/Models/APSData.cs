using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Models
{
    public class APSData
    {
        public string ReturnStatus { get; set; } = string.Empty;
        public string ReturnMessage { get; set; } = string.Empty;
        public string KeyID { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string mimeType { get; set; } = string.Empty;
        public string TotalRecord { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }
}
