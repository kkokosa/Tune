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
            set { Set(nameof(ScriptText), ref this.scriptText, value); this.RunScriptCommand?.RaiseCanExecuteChanged(); }
        }

        public string ScriptArgument
        {
            get { return this.scriptArgument; }
            set { Set(nameof(ScriptArgument), ref this.scriptArgument, value); }
        }

        public string LogText
        {
            get { return this.logText; }
            private set { Set(nameof(LogText), ref this.logText, value); }
        }

        public MainViewModelState State
        {
            get { return this.state; }
            set { Set(nameof(State), ref this.state, value); this.RunScriptCommand?.RaiseCanExecuteChanged(); }
        }

        public string IlText
        {
            get { return this.ilText; }
            private set { Set(nameof(IlText), ref this.ilText, value); }
        }
        public string AsmText
        {
            get { return this.asmText; }
            private set { Set(nameof(AsmText), ref this.asmText, value); }
        }

        public DiagnosticAssemblyMode AssemblyMode
        {
            get { return this.assemblyMode; }
            private set { Set(nameof(AssemblyMode), ref this.assemblyMode, value); }
        }
        public DiagnosticAssembyPlatform AssemblyPlatform
        {
            get { return this.assembyPlatform; }
            private set { Set(nameof(AssemblyPlatform), ref this.assembyPlatform, value); }
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
                this.IlText = assembly.DumpIL();
                this.AsmText = assembly.DumpASM();
                UpdateLog("Script processing ended.");
                return true;
            }
            catch (Exception ex)
            {
                UpdateLog(ex.ToString());
                return false;
            }

        }

        private void UpdateLog(string str)
        {
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