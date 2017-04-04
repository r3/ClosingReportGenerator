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
        private static string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        static void Main(string[] args)
        {
            List<TimeTracker> trackers = new List<TimeTracker>()
            {
                new TimeTracker("Inbound", (x) => x.Direction == CommDirection.Inbound && x.WasReceived),
                new TimeTracker("Outbound", (x) => x.Direction == CommDirection.Outbound),
                new TimeTracker("Abandoned", (x) => x.WasReceived == false)
            };

            Accounts accounts = new Accounts(sentinel, trackers);

            new CommunicationProcessor(builderMeth: CommunicationFactories.fromInboundRecord, adderMeth: accounts.AddCommunication, csvPath: @"C:\inbounds.csv").ProcessCalls();
            new CommunicationProcessor(builderMeth: CommunicationFactories.fromOutboundRecord, adderMeth: accounts.AddCommunication, csvPath: @"C:\outbounds.csv").ProcessCalls();
            new CommunicationProcessor(builderMeth: CommunicationFactories.fromAbandonedRecord, adderMeth: accounts.AddCommunication, csvPath: @"C:\abandons.csv").ProcessCalls();

            string barChartDestination = Path.Combine(desktop, @"barChart.png");
            IView barChart = new BarChartView();
            barChart.AddAccounts(accounts);
            barChart.SaveToFile(barChartDestination);

            string lineChartDestination = Path.Combine(desktop, @"lineChart.png");
            IView lineChart = new LineChartView();
            lineChart.AddAccounts(accounts);
            lineChart.SaveToFile(lineChartDestination);

            string htmlDestination = Path.Combine(desktop, @"view.html");
            IView htmlView = new HtmlView();
            htmlView.AddAccounts(accounts);
            htmlView.SaveToFile(htmlDestination);
        }
    }

    class CommunicationProcessor
    {
        private Func<string[], ICommunication> builderMeth;
        private Action<ICommunication> adderMeth;
        private string csvPath;
        private bool skipHeader;

        public CommunicationProcessor(Func<string[], ICommunication> builderMeth, Action<ICommunication> adderMeth, string csvPath, bool? skipHeader=null)
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
                ICommunication call = builderMeth(record);
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