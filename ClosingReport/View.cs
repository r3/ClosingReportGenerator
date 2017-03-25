using System;
using System.Collections.Generic;
using System.IO;
using OxyPlot;
using OxyPlot.Series;
using RazorEngine;
using RazorEngine.Templating;
using System.Configuration;

namespace ClosingReport
{
    class ChartView
    {
        private PlotModel Model
        {
            get; set;
        }

        public ChartView(IEnumerable<IDataPointProvider> dataToPlot)
        {
            Model = new PlotModel { Title = "Closing Report" };
        }
    }

    class HtmlView
    {
        private static string templatePath = @"View.template";

        public string ResultString
        {
            get;
            private set;
        }

        public HtmlView(Accounts accounts)
        {
            Render(accounts);
        }

        private void Render(Accounts accounts)
        {
            if (!File.Exists(templatePath))
            {
                throw new ArgumentException($"Could not open the template file at, '{templatePath}'");
            }

            string template;
            using (StreamReader reader = new StreamReader(templatePath))
            {
                template = reader.ReadToEnd();
            }

            ResultString = Engine.Razor.RunCompile(
                templateSource: template,
                name: "ClosingReportKey",
                modelType: null,
                model: new
                {
                    Statistics = accounts.Statistics(),
                    Totals = new
                    {
                        TotalReceived = accounts.TotalCount,
                        Inbound = accounts.InboundCount,
                        Outbound = accounts.OutboundCount,
                        AbandonRate = accounts.AbandonedRate
                    }
                }
            );
        }

        public void SaveToFile()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string destination = Path.Combine(desktop, @"view.html");
            using (StreamWriter writer = new StreamWriter(destination))
            {
                writer.Write(ResultString);
            }
        }
    }
}