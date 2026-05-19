using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Models
{
    public class AmloEndpoint
    {
        public string Name { get; set; }
        public string VersionEndpoint { get; set; }
        public string DataEndpoint { get; set; }
        public string ListName { get; set; }

    }
    public class AmloConfig
    {
        public List<AmloEndpoint> Endpoints { get; set; }
    }
}
