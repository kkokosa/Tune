using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tune.UI.MVVM.Services;

namespace Tune.UI.WPF.Services
{
    class XmlExperimentFile : IExperimentFile
    {
        public string Path { get; set; }
        public string Script { get; set; }
        public string ScriptArgument { get; set; }
    }
}
