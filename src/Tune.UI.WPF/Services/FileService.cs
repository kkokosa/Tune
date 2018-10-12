using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Win32;
using Tune.UI.MVVM.Services;
using System.Xml.Linq;

namespace Tune.UI.WPF.Services
{
    class FileService : IFileService
    {
        public string OpenFileDialog()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            openFileDialog.DefaultExt = "xml";
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            if (openFileDialog.ShowDialog() == true)
                return openFileDialog.FileName;
            else
                return string.Empty;
        }

        public string SaveFileDialog()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            if (saveFileDialog.ShowDialog() == true)
                return saveFileDialog.FileName;
            else
                return string.Empty;
        }

        public IExperimentFile LoadExperimentFile(string path)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(path);
            return new XmlExperimentFile()
            {
                Path = path,
                Script = xmlDoc.DocumentElement.SelectSingleNode("/Experiment/Script").InnerText,
                ScriptArgument = xmlDoc.DocumentElement.SelectSingleNode("/Experiment/ScriptArgument").InnerText.Trim()
            };
        }

        public IExperimentFile CreateEmptyExperimentFile(string path)
        {
            return new XmlExperimentFile()
            {
                Path = path,
                Script = string.Empty,
                ScriptArgument = string.Empty
            };
        }

        public void SaveExperimentFile(IExperimentFile file)
        {
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("Experiment",
                    new XElement("Script",
                        new XCData(file.Script)),
                    new XElement("ScriptArgument",
                        new XCData(file.ScriptArgument))));
            doc.Save(file.Path);
        }
    }
}
