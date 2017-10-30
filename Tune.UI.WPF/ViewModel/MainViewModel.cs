using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Tune.Core;

namespace Tune.UI.WPF.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private DiagnosticEngine engine;
        private readonly Assembly mainAssembly;
        private string scriptText;
        private string logText;
        private string ilText;
        private string asmText;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            this.mainAssembly = Assembly.GetEntryAssembly();
            this.RunScriptCommand = new RelayCommand(RunScript, CanRunScript);
            this.engine = new DiagnosticEngine();
            this.engine.Log += UpdateLog;
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

        public string ScriptText
        {
            get { return this.scriptText; }
            set { this.scriptText = value; RaisePropertyChanged(nameof(ScriptText)); this.RunScriptCommand.RaiseCanExecuteChanged(); }
        }

        public string LogText
        {
            get { return this.logText; }
            private set { this.logText = value; RaisePropertyChanged(nameof(LogText)); }
        }

        public string IlText
        {
            get { return this.ilText; }
            private set { this.ilText = value; RaisePropertyChanged(nameof(IlText)); }
        }
        public string AsmText
        {
            get { return this.asmText; }
            private set { this.asmText = value; RaisePropertyChanged(nameof(AsmText)); }
        }

        public RelayCommand RunScriptCommand { get; private set; }

        private void RunScript()
        {
            ThreadPool.QueueUserWorkItem((obj) => RunAsync(obj), Tuple.Create(ScriptText, "<Argument>"));
        }

        private bool CanRunScript()
        {
            return !string.IsNullOrWhiteSpace(this.ScriptText);
        }

        private void RunAsync(object parameters)
        {
            var tuple = parameters as Tuple<string, string>;
            var script = tuple.Item1;
            var argument = tuple.Item2;
            //var level = cbMode.SelectedItem.ToString() == "Release" ? DiagnosticAssemblyMode.Release : DiagnosticAssemblyMode.Debug;
            var level = DiagnosticAssemblyMode.Release;
            var platform = DiagnosticAssembyPlatform.x64;

            try
            {
                var assembly = engine.Compile(script, level, platform);
                string result = assembly.Execute(argument);
                string ilText = assembly.DumpIL();
                UpdateIL(ilText);
                string asmText = assembly.DumpASM();
                UpdateASM(asmText);
                UpdateLog("Script processing ended.");
            }
            catch (Exception ex)
            {
                UpdateLog(ex.ToString());
            }

        }

        private void UpdateIL(string str)
        {
            if (!Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(() => UpdateIL(str));
                return;
            }
            IlText = str;
        }

        private void UpdateASM(string str)
        {
            if (!Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(() => UpdateASM(str));
                return;
            }
            AsmText = str;
        }

        private void UpdateLog(string str)
        {
            if (!Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(() => UpdateLog(str));
                return;
            }
            bool printTime = true;
            string log = printTime
                ? $"[{DateTime.Now:hh:mm:ss.fff}] {str}{Environment.NewLine}"
                : $"{str}{Environment.NewLine}";
            LogText += log;
        }
    }
}