using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tune.UI.MVVM.Services
{
    public interface IFileService
    {
        string OpenFileDialog();
        string SaveFileDialog();

        IExperimentFile LoadExperimentFile(string path);

        IExperimentFile CreateEmptyExperimentFile(string path);

        void SaveExperimentFile(IExperimentFile file);
    }

    public interface IExperimentFile
    {
        string Path { get; }
        string Script { get; set; }
        string ScriptArgument { get; set; }
    }
}
