using OxyPlot.Series;
using System;
using System.Collections;
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
            Dictionary<object, TimeTracker> trackers = new Dictionary<object, TimeTracker>()
            {
                { "Inbound", new TimeTracker(name: "Inbound", trackingCondition: (x) => x.Direction == CommDirection.Inbound && x.WasReceived == true) },
                { "Outbound",  new TimeTracker(name: "Outbound", trackingCondition: (x) => x.Direction == CommDirection.Outbound) },
                { "Abandoned", new TimeTracker(name: "Abandoned", trackingCondition: (x) => x.WasReceived == false) }
            };

            Accounts accounts = new Accounts(sentinel, trackers);

            string root = ConfigurationManager.AppSettings["ResourcePath"];
            new CommunicationProcessor(CommunicationFactories.fromInboundRecord, accounts.AddCommunication, Path.Combine(root, @"inbounds.csv")).ProcessCalls();
            new CommunicationProcessor(CommunicationFactories.fromOutboundRecord, accounts.AddCommunication, Path.Combine(root, @"outbounds.csv")).ProcessCalls();
            new CommunicationProcessor(CommunicationFactories.fromAbandonedRecord, accounts.AddCommunication, Path.Combine(root, @"abandons.csv")).ProcessCalls();

            string outputPath = ConfigurationManager.AppSettings["OutputPath"];
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (outputPath != "Desktop")
            {
                path = outputPath;
            }

            BarChartView barChart = new BarChartView(new AccountsBarChartAdapter(accounts, AccountsBarChartAdapter.SeriesCtor));
            barChart.SaveToFile(Path.Combine(path, @"barChart.png"));

            LineChartView lineChart = new LineChartView(new TimeTrackersLineChartAdapter(trackers.Values, TimeTrackersLineChartAdapter.SeriesCtor));
            lineChart.SaveToFile(Path.Combine(path, @"lineChart.png"));

            HtmlView htmlView = new HtmlView(new AccountsHtmlAdapter(accounts, AccountsHtmlAdapter.SeriesCtor));
            htmlView.SaveToFile(Path.Combine(path, @"view.html"));
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