using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace Tune.Core.Collectors
{
    public class ClrEtwGenerationData
    {
        public DateTime TimeStamp { get; set; }
        public ulong Start { get; set; }
        public ulong Used { get; set; }
        public ulong Reserved { get; set; }
        public EventIndex Index { get; set; }
    }
}
