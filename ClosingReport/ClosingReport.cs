using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;


namespace ClosingReport
{
    class ClosingReport
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
}