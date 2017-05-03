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
            CommunicationProcessor processor = new CommunicationProcessor();
            processor.RegisterCallback(accounts.AddCommunication);

            var cfg = ConfigurationManager.GetSection("resources") as ResourcesConfiguration;
            foreach (ResourceElement resource in cfg.Resources)
            {
                processor.ProcessResource(resource);
            }
            
            string outputPath = ConfigurationManager.AppSettings["OutputPath"];
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (outputPath != "Desktop")
            {
                path = outputPath;
            }

            BarChartView barChart = new BarChartView(new AccountsBarChartAdapter(accounts, AccountsBarChartAdapter.SeriesCtor));
            var barChartPath = Path.Combine(path, @"barChart.png");
            barChart.SaveToFile(barChartPath);

            LineChartView lineChart = new LineChartView(new TimeTrackersLineChartAdapter(trackers.Values, TimeTrackersLineChartAdapter.SeriesCtor));
            var lineChartPath = Path.Combine(path, @"lineChart.png");
            lineChart.SaveToFile(lineChartPath);

            HtmlView htmlView = new HtmlView(new AccountsHtmlAdapter(accounts, AccountsHtmlAdapter.SeriesCtor));
            htmlView.SaveToFile(Path.Combine(path, @"view.html"));

            ViewMailer mailer = new ViewMailer(htmlView.AsCode);
            mailer.ImbedImageAtId("barChart", barChartPath);
            mailer.ImbedImageAtId("lineChart", lineChartPath);
            //mailer.SendMail(new MailClient().Client);
        }
    }
}