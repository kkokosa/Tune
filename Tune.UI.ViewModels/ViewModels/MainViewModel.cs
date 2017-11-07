using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Helpers;
using LiveCharts.Wpf;
using Tune.Core;
using Tune.UI.MVVM.Services;

namespace Tune.UI.MVVM.ViewModels
{
    public class DateViewModel
    {
        public System.DateTime DateTime { get; set; }
        public double Value { get; set; }
        public string Description { get; set; }
        public object Tag { get; set; }

        public string Moment => DateTime.ToString("hh:mm:ss.ffff");
    }

    public class MainViewModel : ViewModelBase
    {
        private DiagnosticEngine engine;
        private readonly Assembly mainAssembly;
        private IExperimentFile currentExperimentFile;
        private string scriptText;
        private string scriptArgument;
        private string logText;
        private string ilText;
        private string asmText;
        private DiagnosticAssemblyMode assemblyMode;
        private DiagnosticAssembyPlatform assembyPlatform;
        private MainViewModelState state;
        private DateViewModel gcSelectedEvent;

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
            this.AssemblyPlatform = Environment.Is64BitProcess
                ? DiagnosticAssembyPlatform.x64
                : DiagnosticAssembyPlatform.x86;
            this.engine = new DiagnosticEngine();
            this.engine.Log += UpdateLog;

            // Commands
            this.RunScriptCommand = new RelayCommand(RunScript, CanRunScript);
            this.ExitCommand = new RelayCommand(Exit);
            this.LoadExperimentCommand = new RelayCommand(LoadExperiment);
            this.SaveExperimentCommand = new RelayCommand(SaveExperiment, CanSaveExperiment);
            this.SaveExperimentAsCommand = new RelayCommand(SaveExperimentAs);
            this.GCDataClickCommand = new RelayCommand<ChartPoint>(GCDataClick);

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
            Messenger.Default.Register<PropertyChangedMessage<IExperimentFile>>(
                this, e =>
                {
                    this.SaveExperimentCommand?.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(Title));
                });

            // LiveCharts customization
            this.mapper = Mappers.Xy<DateViewModel>()
                .X(dayModel => dayModel.DateTime.Ticks)
                .Y(dayModel => dayModel.Value);

            this.GraphDataGC = new SeriesCollection();
            this.GCSections = new SectionsCollection();
            this.GCSectionsLabels = new VisualElementsCollection();
        }

        private void GCDataClick(ChartPoint obj)
        {
            //this.GCSelectedEvent = this.GCEvents.First();// obj.Instance as DateViewModel;
        }

        public string Title
        {
            get
            {
                Version version = this.mainAssembly.GetName().Version;
                var titleAttribute = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(this.mainAssembly,
                    typeof(AssemblyTitleAttribute));
                var experimentPatSuffix = CurrentExperimentFile != null ? $" - {CurrentExperimentFile.Path}" : string.Empty;
                return $"{titleAttribute.Title} {version}{experimentPatSuffix}";
            }
        }

        public IExperimentFile CurrentExperimentFile
        {
            get { return this.currentExperimentFile; }
            set { Set(nameof(CurrentExperimentFile), ref this.currentExperimentFile, value, broadcast: true); }
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
            set { Set(nameof(LogText), ref this.logText, value); }
        }

        public MainViewModelState State
        {
            get { return this.state; }
            set { Set(nameof(State), ref this.state, value, broadcast: true); }
        }

        public string IlText
        {
            get { return this.ilText; }
            set { Set(nameof(IlText), ref this.ilText, value); }
        }
        public string AsmText
        {
            get { return this.asmText; }
            set { Set(nameof(AsmText), ref this.asmText, value); }
        }

        public DiagnosticAssemblyMode AssemblyMode
        {
            get { return this.assemblyMode; }
            set { Set(nameof(AssemblyMode), ref this.assemblyMode, value); }
        }
        public DiagnosticAssembyPlatform AssemblyPlatform
        {
            get { return this.assembyPlatform; }
            set { Set(nameof(AssemblyPlatform), ref this.assembyPlatform, value); }
        }

        public SeriesCollection GraphDataGC
        {
            get;
            set;
        }

        public SectionsCollection GCSections
        {
            get;
            set;
        }

        public VisualElementsCollection GCSectionsLabels
        {
            get;
            set;
        }

        public ObservableCollection<Core.Collectors.ClrEtwGcData> GCEvents
        {
            get;
            set;
        }

        public DateViewModel GCSelectedEvent
        {
            get { return this.gcSelectedEvent; }
            set { Set(nameof(GCSelectedEvent), ref this.gcSelectedEvent, value); }
        }

        public RelayCommand RunScriptCommand { get; private set; }
        public RelayCommand LoadExperimentCommand { get; private set; }
        public RelayCommand SaveExperimentCommand { get; private set; }
        public RelayCommand SaveExperimentAsCommand { get; private set; }
        public RelayCommand ExitCommand { get; private set; }
        public RelayCommand<ChartPoint> GCDataClickCommand { get; set; }

        private async void RunScript()
        {
            LogText = string.Empty;
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

        private bool CanRunScript()
        {
            return !string.IsNullOrWhiteSpace(this.ScriptText) && this.state != MainViewModelState.Running;
        }

        private void LoadExperiment()
        {
            var path = fileService.OpenFileDialog();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var file = fileService.LoadExperimentFile(path);
                this.ScriptText = file.Script;
                this.ScriptArgument = file.ScriptArgument;
                this.CurrentExperimentFile = file;
            }
        }

        private void SaveExperiment()
        {
            this.currentExperimentFile.Script = this.scriptText;
            this.currentExperimentFile.ScriptArgument = this.scriptArgument;
            fileService.SaveExperimentFile(this.CurrentExperimentFile);
        }

        private bool CanSaveExperiment()
        {
            return CurrentExperimentFile != null;
        }

        private void SaveExperimentAs()
        {
            var path = fileService.SaveFileDialog();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var file = fileService.CreateEmptyExperimentFile(path);
                file.Script = this.scriptText;
                file.ScriptArgument = this.scriptArgument;
                fileService.SaveExperimentFile(file);
                this.CurrentExperimentFile = file;
            }
        }

        private void Exit()
        {
            this.applicationService.Exit();
        }


        private async Task<bool> RunAsync(string script, string argument, DiagnosticAssemblyMode level, DiagnosticAssembyPlatform platform)
        {
            try
            {
                var assembly = engine.Compile(script, level, platform);
                string result = assembly.Execute(argument);
                this.IlText = assembly.DumpIL();
                this.AsmText = assembly.DumpASM();
                var dataGen0 = assembly.HeapStatsData.Select(x => new DateViewModel() { DateTime = x.TimeStamp, Value = x.GenerationSize0, Tag = x});
                var dataGen1 = assembly.HeapStatsData.Select(x => new DateViewModel() { DateTime = x.TimeStamp, Value = x.GenerationSize1, Tag = x });
                var dataGen2 = assembly.HeapStatsData.Select(x => new DateViewModel() { DateTime = x.TimeStamp, Value = x.GenerationSize2, Tag = x });
                var gcs = assembly.GcData.Select(x => new AxisSection()
                {
                    SectionOffset = (double)x.TimeStamp.Subtract(TimeSpan.FromMilliseconds(1.0)).Ticks,
                    SectionWidth = (double)TimeSpan.FromMilliseconds(1.0).Ticks
                });
                var gcsLabels = assembly.GcData.Select(x => new VisualElement
                {
                    X = (double) x.TimeStamp.Ticks,
                    Y = 0.0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    UIElement = new Button() { Content = x.Generation.ToString(), Background = Brushes.White }

                });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var seriesGen0 = new LineSeries(mapper) { Title= "Gen0", LineSmoothness = 0, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6 };
                    seriesGen0.Values = dataGen0.AsChartValues();
                    var seriesGen1 = new LineSeries(mapper) { Title = "Gen1", LineSmoothness = 0, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6 };
                    seriesGen1.Values = dataGen1.AsChartValues();
                    var seriesGen2 = new LineSeries(mapper) { Title = "Gen2", LineSmoothness = 0, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6 };
                    seriesGen2.Values = dataGen2.AsChartValues();
                    GraphDataGC.Clear();
                    GraphDataGC.Add(seriesGen0);
                    GraphDataGC.Add(seriesGen1);
                    GraphDataGC.Add(seriesGen2);

                    GCSections.Clear();
                    GCSections.AddRange(gcs);

                    GCSectionsLabels.Clear();
                    GCSectionsLabels.AddRange(gcsLabels);
                }
                );
                this.GCEvents = new ObservableCollection<Core.Collectors.ClrEtwGcData>(assembly.GcData);



                this.RaisePropertyChanged(nameof(GCEvents));
                //
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

        public Func<double, string> XFormatter {
            get
            {
                return value => new System.DateTime((long) ((value < 0.0 ? 0.0 : value))).ToString("hh:mm:ss.ffff");
            } 
        }

        public Func<double, string> YFormatter
        {
            get
            {
                var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                nfi.NumberGroupSeparator = " ";
                return value => value.ToString("#,0", nfi);
            }
        }
    }

    public enum MainViewModelState
    {
        Idle,
        Running
    }
}