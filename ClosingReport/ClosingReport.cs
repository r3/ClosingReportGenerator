using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace ClosingReport
{
    class ClosingReport
    {
        internal static TraceSource log = new TraceSource("ClosingReport");
        internal static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        internal static int sentinel = Convert.ToInt32(config.AppSettings.Settings["Sentinel"].Value);

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (!File.Exists(args[0]))
                {
                    throw new System.IO.FileNotFoundException($"Could not load configuration at '{args[0]}'");
                }

                var configFileMap = new ExeConfigurationFileMap();
                configFileMap.ExeConfigFilename = args[0];
                config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            }

            Dictionary<object, TimeTracker> trackers = new Dictionary<object, TimeTracker>()
            {
                { "Inbound", new TimeTracker(name: "Inbound", trackingCondition: (x) => x.Direction == CommDirection.Inbound && x.WasReceived == true) },
                { "Outbound",  new TimeTracker(name: "Outbound", trackingCondition: (x) => x.Direction == CommDirection.Outbound) },
                { "Abandoned", new TimeTracker(name: "Abandoned", trackingCondition: (x) => x.WasReceived == false) }
            };

            Accounts accounts = new Accounts(sentinel, trackers, GetFilteredIds());
            CommunicationProcessor processor = new CommunicationProcessor();
            processor.RegisterCallback(accounts.AddCommunication);

            var cfg = config.GetSection("resources") as ResourcesConfiguration;
            foreach (ResourceElement resource in cfg.Resources)
            {
                processor.ProcessResource(resource);
            }

            string outputPath = config.AppSettings.Settings["OutputPath"].Value;
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

            HtmlView htmlView = new HtmlView(new AccountsHtmlAdapter(accounts, AccountsHtmlAdapter.SeriesCtor, sentinel));
            htmlView.SaveToFile(Path.Combine(path, @"view.html"));

            ViewMailer mailer = new ViewMailer(htmlView.AsCode);
            mailer.ImbedImageAtId("barChart", barChartPath);
            mailer.ImbedImageAtId("lineChart", lineChartPath);
            mailer.SendMail(new MailClient().Client);
        }

        public static List<int> GetFilteredIds()
        {
            string unparsed;
            try
            {
                unparsed = config.AppSettings.Settings["FilterIds"].Value;
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to read value, 'filterIds' from configuration file with error: {e.Message}");
            }

            try
            {
                return (unparsed.Split(',')).Select(x => Convert.ToInt32(x.Trim())).ToList();
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to convert '{unparsed}' to List of Int32. Please use format, '110,23,91,4' in config file. Got error: {e.Message}");
            }
        }
    }
}