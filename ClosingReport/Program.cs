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

            HtmlView view = new HtmlView(accounts);
            view.SaveToFile();

            /*
            foreach (var account in accounts)
            {
                var inboundTracker = new TimeManagement();
                inboundTracker.AddTimes(account.InboundTimes);

                var outboundTracker = new TimeManagement();
                //...
            }
            */
        }
    }

    class CallProcessor<T> where T : Call
    {
        private Func<string[], T> builderMeth;
        private Action<T> adderMeth;
        private string csvPath;
        private bool skipHeader;

        public CallProcessor(Func<string[], T> builderMeth, Action<T> adderMeth, string csvPath, bool? skipHeader=null)
        {
            this.builderMeth = builderMeth;
            this.adderMeth = adderMeth;
            this.csvPath = csvPath;

            if (!skipHeader.HasValue)
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
                adderMeth(call);
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