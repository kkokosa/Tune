using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using System;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using Tune.Core;
using Tune.UI.MVVM.Services;

namespace Tune.UI.MVVM.ViewModels
{
    public class DateViewModel
    {
        public System.DateTime DateTime { get; set; }
        public double Value { get; set; }
    }

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
        private IApplicationService applicationService;

        private IPointEvaluator<DateViewModel> mapper;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(IApplicationService applicationService, IFileService fileService)
        {
            // Services
            this.fileService = fileService;
            this.applicationService = applicationService;

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

            // Self-register messages
            Messenger.Default.Register<PropertyChangedMessage<string>>(
                this, (e) =>
                {
                    if (e.PropertyName == nameof(ScriptText))
                        this.RunScriptCommand?.RaiseCanExecuteChanged();
                });
            Messenger.Default.Register<PropertyChangedMessage<MainViewModelState>>(
                this, (e) =>
                {
                    this.RunScriptCommand?.RaiseCanExecuteChanged();
                });

            // LiveCharts customization
            this.mapper = Mappers.Xy<DateViewModel>()
                .X(dayModel => (double)dayModel.DateTime.Ticks / TimeSpan.FromHours(1).Ticks)
                .Y(dayModel => dayModel.Value);
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
            set { Set(nameof(ScriptText), ref this.scriptText, value, broadcast: true); }
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
            set { Set(nameof(State), ref this.state, value, broadcast: true); }
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

        public SeriesCollection GraphDataGC
        {
            get
            {
                return new SeriesCollection(mapper)
                {
                    new LineSeries
                    {
                        Values = new ChartValues<DateViewModel>
                        {
                            new DateViewModel
                            {
                                DateTime = System.DateTime.Now,
                                Value = 5
                            },
                            new DateViewModel
                            {
                                DateTime = System.DateTime.Now.AddSeconds(2),
                                Value = 9
                            }
                        }
                    }
                };
            }
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
            this.applicationService.Exit();
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

        public Func<double, string> Formatter {
            get
            {
                return value => new System.DateTime((long) (value * TimeSpan.FromHours(1).Ticks)).ToString("t");
            } 
        }
    }

    public enum MainViewModelState
    {
        Idle,
        Running
    }
}