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

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            this.mainAssembly = Assembly.GetEntryAssembly();
            this.RunScriptCommand = new RelayCommand(RunScript);
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

        public ICommand RunScriptCommand { get; private set; }

        private void RunScript()
        {
            int x = 1;
        }
    }
}