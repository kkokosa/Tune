using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;

namespace Tune.Core.Collectors
{
    // https://github.com/Microsoft/dotnet-samples/blob/master/Microsoft.Diagnostics.Tracing/TraceEvent/TraceEvent/40_SimpleTraceLog.cs
    class DotNetRuntimeEtwCollector : IDisposable
    {
        private List<DiagnosticDataPoint> generation0SizeSeries = new List<DiagnosticDataPoint>();
        private List<DiagnosticDataPoint> generation1SizeSeries = new List<DiagnosticDataPoint>();
        private List<DiagnosticDataPoint> generation2SizeSeries = new List<DiagnosticDataPoint>();
        private List<DiagnosticDataPoint> garbageCollectionSeries = new List<DiagnosticDataPoint>();
        private const string providerName = "Microsoft-Windows-DotNETRuntime";
        private const string sessioName = "Tune-DotNetRuntimeSession";
        private TraceEventSession session;
        private bool stopped;

        public void Start()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            Task.Run(RunAsync, token);
        }

        public List<DiagnosticDataPoint> Generation0SizeSeries => this.generation0SizeSeries;
        public List<DiagnosticDataPoint> Generation1SizeSeries => this.generation1SizeSeries;
        public List<DiagnosticDataPoint> Generation2SizeSeries => this.generation2SizeSeries;
        public List<DiagnosticDataPoint> GarbageCollectionSeries => this.garbageCollectionSeries;

        private Task RunAsync()
        {
            var eventSourceGuid = TraceEventProviders.GetProviderGuidByName(providerName);
            session = new TraceEventSession(sessioName);
            var options = new TraceEventProviderOptions() { StacksEnabled = true };
            session.EnableProvider(eventSourceGuid, TraceEventLevel.Verbose, ulong.MaxValue, options);
            session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)ClrTraceEventParser.Keywords.Default);
            session.Source.Clr.GCHeapStats += ClrOnGcHeapStats;
            session.Source.Clr.GCStart += ClrOnGcStart;
            session.Source.Clr.GCStop += ClrOnGcStop;
            session.Source.Clr.GCGenerationRange += ClrOnGcGenerationRange;
            session.Source.Process();
            return Task.CompletedTask;
        }

        private void ClrOnGcGenerationRange(GCGenerationRangeTraceData evt)
        {
        }

        private void ClrOnGcStop(GCEndTraceData evt)
        {
            if (!IsTargetProcess(evt)) return;

            garbageCollectionSeries.Add(new DiagnosticDataPoint() {DateTime = evt .TimeStamp, Description = evt.Depth.ToString()} );
        }

        private void ClrOnGcStart(GCStartTraceData evt)
        {
            //var cs = gcStartTraceData.CallStack();
        }

        private void ClrOnGcHeapStats(GCHeapStatsTraceData evt)
        {
            if (!IsTargetProcess(evt)) return;

            generation0SizeSeries.Add(new DiagnosticDataPoint() { DateTime = evt.TimeStamp, Value = evt.GenerationSize0, Description = $"Gen0 {evt.ThreadID}"});
            generation1SizeSeries.Add(new DiagnosticDataPoint() { DateTime = evt.TimeStamp, Value = evt.GenerationSize1 });
            generation2SizeSeries.Add(new DiagnosticDataPoint() { DateTime = evt.TimeStamp, Value = evt.GenerationSize2 });
        }

        public void Stop()
        {
            stopped = true;
            session.Stop();
            session.Dispose();
        }

        public void Dispose()
        {
            if (!stopped)
            {
                Stop();
            }
        }

        private bool IsTargetProcess(TraceEvent evt)
        {
            return Process.GetCurrentProcess().Id == evt.ProcessID;
        }
    }
}
