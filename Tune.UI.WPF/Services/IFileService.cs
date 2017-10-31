using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tune.UI.WPF.Services
{
    public interface IFileService
    {
        string OpenFileDialog(string defaultPath);

        string FileReadToEnd(string path);
    }
}
