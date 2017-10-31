using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Tune.UI.WPF.Services
{
    class FileService : IFileService
    {
        public string OpenFileDialog(string defaultPath)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = defaultPath;
            if (openFileDialog.ShowDialog() == true)
                return openFileDialog.FileName;
            else
                return string.Empty;
        }

        public string FileReadToEnd(string path)
        {
            return File.ReadAllText(path);
        }
    }
}
