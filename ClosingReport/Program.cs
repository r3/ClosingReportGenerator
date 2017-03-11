using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClosingReport
{
    public class ParseException : Exception
    {
        public ParseException()
        {
        }

        public ParseException(string message)
            : base(message)
        {
        }

        public ParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    class ReportRunner
    {
        public static TraceSource log = new TraceSource("ClosingReport");
        public static int sentinel = Convert.ToInt32(ConfigurationManager.AppSettings["Sentinel"]);

        static void Main(string[] args)
        {
            Accounts accounts = new Accounts(sentinel);

            new CallProcessor<InboundCall>(
                builderMeth: InboundCall.fromRecord,
                adderMeth: accounts.AddCall<InboundCall>,
                csvPath: @"C:\inbounds.csv"
            ).ProcessCalls();

            new CallProcessor<OutboundCall>(
                builderMeth: OutboundCall.fromRecord,
                adderMeth: accounts.AddCall<OutboundCall>,
                csvPath: @"C:\outbounds.csv"
            ).ProcessCalls();

            new CallProcessor<AbandonedCall>(
                builderMeth: AbandonedCall.fromRecord,
                adderMeth: accounts.AddCall<AbandonedCall>,
                csvPath: @"C:\abandons.csv"
            ).ProcessCalls();

            foreach (var accountStats in accounts.Statistics())
            {
                log.TraceEvent(TraceEventType.Information, 0, $"\n\nAccount: {accountStats.AccountName}");
                log.TraceEvent(TraceEventType.Information, 0, $"Inbound Average: {accountStats.InboundAverage}");
                log.TraceEvent(TraceEventType.Information, 0, $"Abandoned Average: {accountStats.AbandonedAverage}");
                log.TraceEvent(TraceEventType.Information, 0, $"Inbound Total: {accountStats.TotalInbound}");
                log.TraceEvent(TraceEventType.Information, 0, $"Outbound Total: {accountStats.TotalOutbound}");
                log.TraceEvent(TraceEventType.Information, 0, $"Abandoned Total: {accountStats.TotalAbandoned}");
            }
        }
    }

    class CallProcessor<T> where T : Call
    {
        private Func<string[], T> builderMeth;
        private Action<int, T> adderMeth;
        private string csvPath;
        private bool skipHeader;

        public CallProcessor(Func<string[], T> builderMeth, Action<int, T> adderMeth, string csvPath, bool? skipHeader=null)
        {
            this.builderMeth = builderMeth;
            this.adderMeth = adderMeth;
            this.csvPath = csvPath;

            if (skipHeader == null)
            {
                this.skipHeader = (ConfigurationManager.AppSettings["SkipHeader"] == "true") ? true : false;
            }
            else
            {
                this.skipHeader = (bool)skipHeader;
            }
            
        }

        public void ProcessCalls()
        {
            foreach (string[] record in IterRecords())
            {
                T call = builderMeth(record);
                adderMeth(call.AccountCode, call);
            }
        }

        private IEnumerable<string[]> IterRecords()
        {
            if (!File.Exists(csvPath))
            {
                throw new ArgumentException($"Could not open file at, '{csvPath}'");
            }

            using (var fs = File.OpenRead(csvPath))
            using (var reader = new StreamReader(fs))
            {
                reader.ReadLine();  // Skip header
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] splitted = line.Split(',');

                    yield return splitted.Select(x => x.Trim('"')).ToArray();
                }
            }
            yield break;
        }
    }

}