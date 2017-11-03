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
    class ClrEtwCollector : IDisposable
    {
        private readonly List<ClrEtwGcData> gcData = new List<ClrEtwGcData>();
        private readonly List<ClrEtwHeapStatsData> heapStatsData = new List<ClrEtwHeapStatsData>();
        private const string providerName = "Microsoft-Windows-DotNETRuntime";
        private const string sessioName = "Tune-DotNetRuntimeSession";

        private bool stopped;
        private TraceEventSession session;

        public void Start()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            Task.Run(RunAsync, token);
        }

        public List<ClrEtwHeapStatsData> HeapStatsData => this.heapStatsData;
        public List<ClrEtwGcData> GcData => this.gcData;

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

            gcData.Add(new ClrEtwGcData()
            {
                TimeStamp = evt .TimeStamp,
                Description = evt.Depth.ToString()
            } );
        }

        private void ClrOnGcStart(GCStartTraceData evt)
        {
            //var cs = gcStartTraceData.CallStack();
        }

        private void ClrOnGcHeapStats(GCHeapStatsTraceData evt)
        {
            if (!IsTargetProcess(evt)) return;

            heapStatsData.Add(new ClrEtwHeapStatsData()
            {
                TimeStamp = evt.TimeStamp,
                GenerationSize0 = evt.GenerationSize0,
                GenerationSize1 = evt.GenerationSize1,
                GenerationSize2 = evt.GenerationSize2,
                GenerationSize3 = evt.GenerationSize3,
                Description = $"TID: {evt.ThreadID}"
            });
        }

        public void Stop()
        {
            stopped = true;
            session?.Stop();
            session?.Dispose();
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
