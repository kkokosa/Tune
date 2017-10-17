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
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Mono.Cecil;
using SharpDisasm;
using SharpDisasm.Translators;
using Application = System.Windows.Forms.Application;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Tune.UI
{
    public partial class MainView : DevExpress.XtraEditors.XtraForm
    {
        private NativeTarget nativeTarget;
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
        private ClrRuntime runtime;
        private ulong currentMethodAddress = 0;

        public MainView()
        {
            InitializeComponent();
            if (!mvvmContext1.IsDesignMode)
                InitializeBindings();

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            this.Text += $" {version.ToString()}";

            this.nativeTarget = new NativeTarget(Process.GetCurrentProcess().Id);

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
                long address = Convert.ToInt64(text.Substring(2), 16);
                using (DataTarget target =
                    DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, 5000, AttachFlag.Passive))
                {
                    foreach (ClrInfo version in target.ClrVersions)
                    {
                        ClrRuntime runtime = target.ClrVersions.Single().CreateRuntime();
                        string methodSignature = runtime.GetMethodByAddress(unchecked((ulong) address))
                            ?.GetFullSignature();
                        if (!string.IsNullOrWhiteSpace(methodSignature))
                        {
                            teASM.Text = teASM.Text.Replace(text, methodSignature);
                            return;
                        }
                    }
                }

                Symbol symbol = this.nativeTarget.ResolveSymbol((ulong)address);
                if (!string.IsNullOrWhiteSpace(symbol.MethodName))
                {
                    teASM.Text = teASM.Text.Replace(text, symbol.ToString());
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
            var syntaxTree = CSharpSyntaxTree.ParseText(tuple.Item1);
            UpdateLog("Script parsed.");

            string assemblyName = $"assemblyName_{DateTime.Now.Ticks}";
            OptimizationLevel level = cbMode.SelectedItem.ToString() == "Release"
                ? OptimizationLevel.Release
                : OptimizationLevel.Debug;
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, 
                    optimizationLevel: level,
                    allowUnsafe: true,
                    platform: Platform.X64));
            UpdateLog($"Script compilation into assembly {assemblyName} in {level}.");

            using (var dllStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(dllStream, pdbStream);
                if (!emitResult.Success)
                {
                    var x = emitResult.Diagnostics;
                    UpdateLog($"Script compilation failed: {string.Join(Environment.NewLine, x.Select(d => d.ToString()))}.");
                    return;
                }
                UpdateLog("Script compilation succeeded.");

                dllStream.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(dllStream.ToArray());
                UpdateLog("Dynamic assembly loaded.");

                Type type = assembly.GetTypes().First();
                MethodInfo mi = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).First();
                object obj = Activator.CreateInstance(type);
                UpdateLog($"Object with type {type.FullName} and method {mi.Name} resolved.");

                object result = null;
                try
                {
                    TextWriter programWriter = new StringWriter();
                    Console.SetOut(programWriter);
                    UpdateLog($"Invoking method {mi.Name} with argument {tuple.Item2}");
                    result = mi.Invoke(obj, new object[] { tuple.Item2 });
                    UpdateLog($"Script result: {result}");
                    UpdateLog("Script log:");
                    UpdateLog(programWriter.ToString(), printTime: false);
                }
                catch (Exception e)
                {
                    UpdateLog($"Script execution failed: {e.ToString()}");
                    return;
                }

                // IL
                TextWriter ilWriter = new StringWriter();
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(dllStream);
                var ilOutput = new PlainTextOutput(ilWriter);
                var reflectionDisassembler = new ReflectionDisassembler(ilOutput, false, CancellationToken.None);
                reflectionDisassembler.WriteModuleContents(assemblyDefinition.MainModule);
                UpdateLog("Dynamic assembly disassembled to IL.");
                UpdateIL(ilWriter.ToString());

                // ASM
                using (DataTarget target = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, 5000, AttachFlag.Passive))
                {
                    foreach (ClrInfo clrInfo in target.ClrVersions)
                    {
                        UpdateLog("Found CLR Version:" + clrInfo.Version.ToString());

                        // This is the data needed to request the dac from the symbol server:
                        ModuleInfo dacInfo = clrInfo.DacInfo;
                        UpdateLog($"Filesize:  {dacInfo.FileSize:X}");
                        UpdateLog($"Timestamp: {dacInfo.TimeStamp:X}");
                        UpdateLog($"Dac File:  {dacInfo.FileName}");

                        this.runtime = target.ClrVersions.Single().CreateRuntime();
                        var appDomain = runtime.AppDomains[0];
                        var module = appDomain.Modules.LastOrDefault(m => m.AssemblyName != null && m.AssemblyName.StartsWith(assemblyName));
                        TextWriter asmWriter = new StringWriter();
                        asmWriter.WriteLine(
                            $"; {clrInfo.ModuleInfo.ToString()} ({clrInfo.Flavor} {clrInfo.Version})");
                        asmWriter.WriteLine(
                            $"; {clrInfo.DacInfo.FileName} ({clrInfo.DacInfo.TargetArchitecture} {clrInfo.DacInfo.Version})");
                        asmWriter.WriteLine();
                        foreach (var typeClr in module.EnumerateTypes())
                        {
                            asmWriter.WriteLine($"; Type {typeClr.Name}");

                            ClrHeap heap = runtime.Heap;
                            ClrType @object = heap.GetTypeByMethodTable(typeClr.MethodTable);

                            foreach (ClrMethod method in @object.Methods)
                            {
                                MethodCompilationType compileType = method.CompilationType;
                                ArchitectureMode mode = clrInfo.DacInfo.TargetArchitecture == Architecture.X86
                                    ? ArchitectureMode.x86_32
                                    : ArchitectureMode.x86_64;

                                this.currentMethodAddress = 0;
                                var translator2 = new IntelTranslator
                                {
                                    SymbolResolver = (Instruction instruction, long addr, ref long offset) =>
                                        ResolveSymbol(runtime, instruction, addr, ref currentMethodAddress)
                                };
                                var translator = new IntelTranslator();
                                translator.SymbolResolver = AsmSymbolResolver;

                                // This not work even ClrMd says opposite...
                                //ulong startAddress = method.NativeCode;
                                //ulong endAddress = method.ILOffsetMap.Select(entry => entry.EndAddress).Max();

                                DisassembleAndWrite(method, mode, translator2, ref currentMethodAddress, asmWriter);
                                UpdateLog($"Method {method.Name} disassembled to ASM.");
                                asmWriter.WriteLine();
                            }
                        }
                        UpdateASM(asmWriter.ToString());
                        break;
                    }
                }
                UpdateLog("Script processing ended.");
            }
        }

        private string AsmSymbolResolver(Instruction instruction, long addr, ref long offset)
        {
            var operand = instruction.Operands.Length > 0 ? instruction.Operands[0] : null;
            if (operand?.PtrOffset == 0)
            {
                var baseOffset = instruction.PC - currentMethodAddress;
                return $"L{baseOffset + operand.PtrSegment:x4}";
            }

            string signature = runtime.GetMethodByAddress(unchecked((ulong)addr))?.GetFullSignature();
            if (!string.IsNullOrWhiteSpace(signature))
                return signature;
            Symbol symbol = this.nativeTarget.ResolveSymbol((ulong)addr);
            return symbol.ToString();
        }

        private void UpdateLog(string str, bool printTime = true)
        {
            if (tbResult.InvokeRequired)
            {
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

        private void DisassembleAndWrite(ClrMethod method, ArchitectureMode architecture, Translator translator, ref ulong methodAddressRef, TextWriter writer)
        {
            writer.WriteLine(method.GetFullSignature());
            var info = FindNonEmptyHotColdInfo(method);
            if (info == null)
            {
                writer.WriteLine("    ; Failed to find HotColdInfo");
                return;
            }
            var methodAddress = info.HotStart;
            methodAddressRef = methodAddress;
            using (var disasm = new Disassembler(new IntPtr(unchecked((long)methodAddress)), (int)info.HotSize, architecture, methodAddress))
            {
                foreach (var instruction in disasm.Disassemble())
                {
                    writer.Write(String.Format("0x{0:X8}`{1:X8}:", (instruction.Offset >> 32) & 0xFFFFFFFF, instruction.Offset & 0xFFFFFFFF));
                    writer.Write("    L");
                    writer.Write((instruction.Offset - methodAddress).ToString("x4"));
                    writer.Write(": ");
                    writer.WriteLine(translator.Translate(instruction));
                }
            }
        }

        private HotColdRegions FindNonEmptyHotColdInfo(ClrMethod method)
        {
            // I can't really explain this, but it seems that some methods 
            // are present multiple times in the same type -- one compiled
            // and one not compiled. A bug in clrmd?
            if (method.HotColdInfo.HotSize > 0)
                return method.HotColdInfo;

            if (method.Type == null)
                return null;

            var methodSignature = method.GetFullSignature();
            foreach (var other in method.Type.Methods)
            {
                if (other.MetadataToken == method.MetadataToken && other.GetFullSignature() == methodSignature && other.HotColdInfo.HotSize > 0)
                    return other.HotColdInfo;
            }

            return null;
        }
        private string ResolveSymbol(ClrRuntime runtime, Instruction instruction, long addr, ref ulong currentMethodAddress)
        {
            var operand = instruction.Operands.Length > 0 ? instruction.Operands[0] : null;
            if (operand?.PtrOffset == 0)
            {
                var baseOffset = instruction.PC - currentMethodAddress;
                return $"L{baseOffset + operand.PtrSegment:x4}";
            }

            string signature = runtime.GetMethodByAddress(unchecked((ulong)addr))?.GetFullSignature();
            if (!string.IsNullOrWhiteSpace(signature))
                return signature;
            Symbol symbol = this.nativeTarget.ResolveSymbol((ulong)addr);
            if (!string.IsNullOrWhiteSpace(symbol.MethodName))
                return symbol.ToString();
            return null;
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
