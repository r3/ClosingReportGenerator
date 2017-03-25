using System;
using System.Collections.Generic;
using System.IO;
using OxyPlot;
using OxyPlot.Series;
using RazorEngine;
using RazorEngine.Templating;

namespace ClosingReport
{
    class ChartView
    {
        public PlotModel Model
        {
            get;
            set;
        }

        public ChartView(IEnumerable<IDataPointProvider> dataToPlot)
        {
            Model = new PlotModel { Title = "Closing Report" };
        }
    }

    class HtmlView
    {
        private static string templatePath = @"View.template";

        private Accounts toPlot
        {
            get;
            set;
        }

        public HtmlView(Accounts accountsToPlot)
        {
            toPlot = accountsToPlot;
        }

        public void render()
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

            string results = Engine.Razor.RunCompile(
                templateSource: template,
                name: "ClosingReportKey",
                modelType: null,
                model: new
                {
                    Statistics = toPlot.Statistics(),
                    Totals = new
                    {
                        TotalReceived = toPlot.TotalCount,
                        Inbound = toPlot.InboundCount,
                        Outbound = toPlot.OutboundCount,
                        AbandonRate = toPlot.AbandonedRate
                    }
                }
            );
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string destination = Path.Combine(desktop, @"view.html");
            using (StreamWriter writer = new StreamWriter(destination))
            {
                writer.Write(results);
            }
        }
    }
}
