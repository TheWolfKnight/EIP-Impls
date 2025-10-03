using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Control_Channel.Models
{
    public class ControlEnvelope
    {
        public required string Action { get; set; } // upsert | remove | replace
        public required List<Destination> Rules { get; set; }
    }
}
