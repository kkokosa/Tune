using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using DevExpress.XtraCharts;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Application = System.Windows.Forms.Application;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.Forms.MessageBox;
using Tune.Core;

namespace Tune.UI
{
    public partial class MainView : DevExpress.XtraEditors.XtraForm
    {
        const int interval = 50;
        Random random = new Random();
        int TimeInterval = 10;
        private List<SeriesPoint> gen0series = new List<SeriesPoint>();
        private List<SeriesPoint> gen1series = new List<SeriesPoint>();
        private List<SeriesPoint> gen2series = new List<SeriesPoint>();
        private List<SeriesPoint> gen3series = new List<SeriesPoint>();
        private List<ConstantLine> gcseries = new List<ConstantLine>();
        private TextEditor teEditor;
        private TextEditor teIL;
        private TextEditor teASM;

        private DiagnosticEngine engine;

        public MainView()
        {
            InitializeComponent();
            if (!mvvmContext1.IsDesignMode)
                InitializeBindings();

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            this.Text += $" {version.ToString()}";

            this.engine = new DiagnosticEngine();
            this.engine.Log += UpdateLog;

            IHighlightingDefinition asmGiHighlightingDefinition;
            using (TextReader s = new StringReader(Resources.SyntaxHighlightingIL))
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    asmGiHighlightingDefinition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            teEditor = new TextEditor();
            teEditor.Options.ConvertTabsToSpaces = true;
            teEditor.Options.IndentationSize = 3;
            teEditor.ShowLineNumbers = true;
            teEditor.FontFamily = new FontFamily("Consolas");
            teEditor.FontSize = 12.0;
            teEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");
            teEditor.Text = Resources.Samples_Sample0;
            elementHost1.Child = teEditor;

            teIL = new TextEditor();
            teIL.ShowLineNumbers = true;
            teIL.FontFamily = new FontFamily("Consolas");
            teIL.FontSize = 12.0;
            teIL.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");
            elementHostIL.Child = teIL;

            teASM = new TextEditor();
            teASM.ShowLineNumbers = true;
            teASM.FontFamily = new FontFamily("Consolas");
            teASM.FontSize = 12.0;
            teASM.SyntaxHighlighting = asmGiHighlightingDefinition;
            var contextMenu = new System.Windows.Controls.ContextMenu();
            var menuItem1 = new System.Windows.Controls.MenuItem();
            menuItem1.Header = "Resolve method...";
            menuItem1.Click += MenuItem1OnClick;
            contextMenu.Items.Add(menuItem1);
            teASM.ContextMenu = contextMenu;
            elementHostASM.Child = teASM;

            ThreadPool.QueueUserWorkItem(ThreadCallback);
            timer1.Interval = interval;
            timer1.Start();
        }

        private void MenuItem1OnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var text = teASM.SelectedText;
            if (text.StartsWith("0x"))
            {
                ulong address = Convert.ToUInt64(text.Substring(2), 16);
                string symbol = engine.ResolveSymbol(address);
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    teASM.Text = teASM.Text.Replace(text, symbol);
                }
            }
        }

        private void ThreadCallback(object state)
        {
            using (var session = new TraceEventSession("MyRealTimeSession"))
            {
                var providerName = "Microsoft-Windows-DotNETRuntime";
                var eventSourceGuid = TraceEventProviders.GetProviderGuidByName(providerName);
                session.EnableProvider(eventSourceGuid);
                session.Source.Clr.GCHeapStats += ClrOnGcHeapStats;
                session.Source.Clr.GCStart += ClrOnGcStart;
                session.Source.Clr.GCStop += Clr_GCStop;
                session.Source.Process();
            }
        }

        private void Clr_GCStop(GCEndTraceData data)
        {
            DateTime now = DateTime.Now;
            int currentPID = Process.GetCurrentProcess().Id;
            if (data.ProcessID == currentPID)
            {
                var line = new ConstantLine(data.Depth.ToString(), now);
                line.Color = Color.LightGray;
                line.ShowInLegend = false;
                gcseries.Add(line);
            }
        }

        private void ClrOnGcStart(GCStartTraceData gcStartTraceData)
        {
        }

        private void ClrOnGcHeapStats(GCHeapStatsTraceData heapStats)
        {
            DateTime now = DateTime.Now;
            int currentPID = Process.GetCurrentProcess().Id;
            if (heapStats.ProcessID == currentPID)
            {
                gen0series.Add(new SeriesPoint(now, heapStats.GenerationSize0));
                gen1series.Add(new SeriesPoint(now, heapStats.GenerationSize1));
                gen2series.Add(new SeriesPoint(now, heapStats.GenerationSize2));
                gen3series.Add(new SeriesPoint(now, heapStats.GenerationSize3));
            }
        }

        void InitializeBindings()
        {
            var fluent = mvvmContext1.OfType<MainViewModel>();
        }

        AxisRange AxisXRange
        {
            get
            {
                SwiftPlotDiagram diagram = chartControl.Diagram as SwiftPlotDiagram;
                if (diagram != null)
                    return diagram.AxisX.Range;
                return null;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            DateTime argument = DateTime.Now;
            DateTime minDate = argument.AddSeconds(-TimeInterval);

            UpdateSeries(0, argument, minDate, gen0series);
            UpdateSeries(1, argument, minDate, gen1series);
            UpdateSeries(2, argument, minDate, gen2series);
            UpdateSeries(3, argument, minDate, gen3series);

            SwiftPlotDiagram diagram = chartControl.Diagram as SwiftPlotDiagram;
            diagram.AxisX.ConstantLines.AddRange(gcseries.ToArray());
            gcseries.Clear();

            if (AxisXRange != null)
            {
                try
                {
                    AxisXRange.SetMinMaxValues(minDate, argument);
                }
                catch
                {
                    // Oh yeah! Let's kill this...
                }
            }
        }

        private void UpdateSeries(int index, DateTime argument, DateTime minDate, List<SeriesPoint> data)
        {
            Series series = chartControl.Series[index];
            if (series == null)
                return;
            int pointsToRemoveCount = 0;
            foreach (SeriesPoint point in series.Points)
                if (point.DateTimeArgument < minDate)
                    pointsToRemoveCount++;
            if (pointsToRemoveCount < series.Points.Count)
                pointsToRemoveCount--;
            series.Points.AddRange(data.ToArray());
            data.Clear();
            if (pointsToRemoveCount > 0)
            {
                series.Points.RemoveRange(0, pointsToRemoveCount);
            }
        }

        private void RunAsync(object parameters)
        {
            var tuple = parameters as Tuple<string, string>;
            var script = tuple.Item1;
            var argument = tuple.Item2;
            var level = cbMode.SelectedItem.ToString() == "Release" ? DiagnosticAssemblyMode.Release : DiagnosticAssemblyMode.Debug;
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

        private void UpdateLog(string str)
        {
            if (tbResult.InvokeRequired)
            {
                bool printTime = true;
                string log = printTime
                    ? $"[{DateTime.Now:hh:mm:ss.fff}] {str}{Environment.NewLine}"
                    : $"{str}{Environment.NewLine}";
                tbResult.Invoke(new Action<string>(result =>
                {
                    tbResult.Text += result;
                    tbResult.ScrollToCaret();
                }), log);
            }
        }

        private void UpdateIL(string str)
        {
            if (elementHostIL.InvokeRequired)
            {
                elementHostIL.Invoke(new Action<string>(result => teIL.Text = result), str);
            }
        }

        private void UpdateASM(string str)
        {
            if (elementHostASM.InvokeRequired)
            {
                elementHostASM.Invoke(new Action<string>(result => teASM.Text = result), str);
            }
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            teASM.Text = string.Empty;
            teIL.Text = string.Empty;
            ThreadPool.QueueUserWorkItem((obj) => RunAsync(obj), Tuple.Create(teEditor.Text, tbInput.Text));
        }

        private void barStaticItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            teEditor.Text = Resources.Samples_Sample1;
        }

        private void barStaticItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {

        }

        private void btnExit_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Application.Exit();
        }

        private void btnSample1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            teEditor.Text = Resources.Samples_Sample1;
        }

        private void btnSample2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            teEditor.Text = Resources.Samples_Sample2;
        }

        private void btnSample3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            teEditor.Text = Resources.Samples_Sample3;
        }
    }
}
