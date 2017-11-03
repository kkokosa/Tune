using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Tune.Core.Collectors
{
    public class ClrEtwGcData
    {
        public DateTime TimeStamp { get; set; }
        public string Description { get; set; }
        public EventIndex Index { get; set; }
        public int Generation { get; set; }
        public GCReason Reason { get; set; }
        public GCType Type { get; set; }
    }
}
