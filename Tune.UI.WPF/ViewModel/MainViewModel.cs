using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Resources;
using System.Windows.Threading;
using Tune.Core;
using Tune.UI.WPF.Services;

namespace Tune.UI.WPF.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private DiagnosticEngine engine;
        private readonly Assembly mainAssembly;
        private string scriptText;
        private string scriptArgument;
        private string logText;
        private string ilText;
        private string asmText;
        private DiagnosticAssemblyMode assemblyMode;
        private DiagnosticAssembyPlatform assembyPlatform;
        private MainViewModelState state;

        private IFileService fileService;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(IFileService fileService)
        {
            // Initial state
            this.scriptArgument = "<Argument>";
            this.state = MainViewModelState.Idle;
            this.mainAssembly = Assembly.GetEntryAssembly();
            this.engine = new DiagnosticEngine();
            this.engine.Log += UpdateLog;

            // Commands
            this.RunScriptCommand = new RelayCommand(RunScript, CanRunScript);
            this.ExitCommand = new RelayCommand(Exit);
            this.LoadScriptCommand = new RelayCommand(LoadScript);

            // Services
            this.fileService = fileService;
        }

        public string Title
        {
            get
            {
                Version version = this.mainAssembly.GetName().Version;
                var titleAttribute = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(this.mainAssembly,
                    typeof(AssemblyTitleAttribute));
                return $"{titleAttribute.Title} {version}";
            }
        }

        public string ScriptText
        {
            get { return this.scriptText; }
            set { this.scriptText = value; RaisePropertyChanged(nameof(ScriptText)); this.RunScriptCommand?.RaiseCanExecuteChanged(); }
        }

        public string ScriptArgument
        {
            get { return this.scriptArgument; }
            set { this.scriptArgument = value; RaisePropertyChanged(nameof(ScriptArgument)); }
        }

        public string LogText
        {
            get { return this.logText; }
            private set { this.logText = value; RaisePropertyChanged(nameof(LogText)); }
        }

        public MainViewModelState State
        {
            get { return this.state; }
            set { this.state = value; RaisePropertyChanged(nameof(State)); this.RunScriptCommand?.RaiseCanExecuteChanged(); }
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

        public DiagnosticAssemblyMode AssemblyMode
        {
            get { return this.assemblyMode; }
            private set { this.assemblyMode = value; RaisePropertyChanged(nameof(AssemblyMode)); }
        }
        public DiagnosticAssembyPlatform AssemblyPlatform
        {
            get { return this.assembyPlatform; }
            private set { this.assembyPlatform = value; RaisePropertyChanged(nameof(AssemblyPlatform)); }
        }

        public RelayCommand RunScriptCommand { get; private set; }
        public RelayCommand LoadScriptCommand { get; private set; }
        public RelayCommand ExitCommand { get; private set; }

        private async void RunScript()
        {
            UpdateLog("Running started.");
            this.State = MainViewModelState.Running;
            var cancellationTokenSource = new CancellationTokenSource();
            var progressReport = new Progress<string>((status) => UpdateLog($"  Running..."));
            var token = cancellationTokenSource.Token;
            var result = await Task.Run(() => RunAsync(this.scriptText, this.scriptArgument, this.assemblyMode, this.assembyPlatform),
                token);
            this.State = MainViewModelState.Idle;
            UpdateLog($"Running ended with success {result}");
        }

        private void LoadScript()
        {
            var path = fileService.OpenFileDialog("C:\\");
            if (!string.IsNullOrWhiteSpace(path))
            {
                this.ScriptText = fileService.FileReadToEnd(path);
            }
        }

        private void Exit()
        {
            Application.Current.MainWindow.Close();
        }

        private bool CanRunScript()
        {
            return !string.IsNullOrWhiteSpace(this.ScriptText) && this.state != MainViewModelState.Running;
        }

        private async Task<bool> RunAsync(string script, string argument, DiagnosticAssemblyMode level, DiagnosticAssembyPlatform platform)
        {
            try
            {
                var assembly = engine.Compile(script, level, platform);
                string result = assembly.Execute(argument);
                string ilText = assembly.DumpIL();
                UpdateIL(ilText);
                string asmText = assembly.DumpASM();
                UpdateASM(asmText);
                UpdateLog("Script processing ended.");
                return true;
            }
            catch (Exception ex)
            {
                UpdateLog(ex.ToString());
                return false;
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

    public enum MainViewModelState
    {
        Idle,
        Running
    }
}