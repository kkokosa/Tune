using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace Tune.Core.Collectors
{
    public class ClrEtwHeapStatsData
    {
        public DateTime TimeStamp { get; set; }
        public long GenerationSize0 { get; set; }
        public long GenerationSize1 { get; set; }
        public long GenerationSize2 { get; set; }
        public long GenerationSize3 { get; set; }
        public string Description { get; set; }
        public EventIndex Index { get; set; }
    }
}
