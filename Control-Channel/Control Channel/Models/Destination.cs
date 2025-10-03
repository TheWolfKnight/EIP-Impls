using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Control_Channel.Models
{
    public class Destination
    {
        public required string SortKey { get; set; }   // fx "order.*" eller "order.#"
        public required string QueueName { get; set; } // fx "A", "B", "C"
    }

}
