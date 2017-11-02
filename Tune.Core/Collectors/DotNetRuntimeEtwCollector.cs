using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;

namespace Tune.Core.Collectors
{
    class DotNetRuntimeEtwCollector : IDisposable
    {
        private List<DiagnosticDataPoint> generation0SizeSeries = new List<DiagnosticDataPoint>();
        private List<DiagnosticDataPoint> generation1SizeSeries = new List<DiagnosticDataPoint>();
        private List<DiagnosticDataPoint> generation2SizeSeries = new List<DiagnosticDataPoint>();
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

        private Task RunAsync()
        {
            var eventSourceGuid = TraceEventProviders.GetProviderGuidByName(providerName);
            session = new TraceEventSession(sessioName);
            session.EnableProvider(eventSourceGuid);
            session.Source.Clr.GCHeapStats += ClrOnGcHeapStats;
            session.Source.Process();
            return Task.CompletedTask;
        }

        private void ClrOnGcHeapStats(GCHeapStatsTraceData evt)
        {
            generation0SizeSeries.Add(new DiagnosticDataPoint() { DateTime = evt.TimeStamp, Value = evt.GenerationSize0 });
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
    }
}
