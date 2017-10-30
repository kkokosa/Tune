using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Tune.UI.WPF.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Assembly mainAssembly;
        private string script;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            this.mainAssembly = Assembly.GetEntryAssembly();
            this.RunScriptCommand = new RelayCommand(RunScript, CanRunScript);
        }

        public string Title
        {
            get
            {
                Version version = this.mainAssembly.GetName().Version;
                var titleAttribute = (AssemblyTitleAttribute) Attribute.GetCustomAttribute(this.mainAssembly,
                    typeof(AssemblyTitleAttribute));
                return $"{titleAttribute.Title} {version}";
            }
        }

        public string Script
        {
            get { return this.script; }
            set { this.script = value; RaisePropertyChanged(nameof(Script)); this.RunScriptCommand.RaiseCanExecuteChanged(); }
        }

        public RelayCommand RunScriptCommand { get; private set; }

        private void RunScript()
        {
            string x = Script;
        }

        private bool CanRunScript()
        {
            return !string.IsNullOrWhiteSpace(this.Script);
        }
    }
}