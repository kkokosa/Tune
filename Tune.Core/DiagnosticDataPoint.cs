using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tune.Core
{
    public class DiagnosticDataPoint
    {
        public System.DateTime DateTime { get; set; }
        public double Value { get; set; }
        public string Description { get; set; }
    }
}
