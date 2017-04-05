using System;
using System.Collections.Generic;
using System.IO;
using RazorEngine;
using RazorEngine.Templating;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Threading;
using RazorEngine.Configuration;

namespace ClosingReport
{
    class BarChartView
    {
        private bool rendered = false;

        private AccountsBarChartAdapter Adapter
        {
            get;
            set;
        }

        private PlotModel Model
        {
            get;
            set;
        }

        private OxyPlot.Axes.CategoryAxis CategoryAxis
        {
            get;
            set;
        }

        private OxyPlot.Axes.LinearAxis ValueAxis
        {
            get;
            set;
        }

        public BarChartView(AccountsBarChartAdapter adapter)
        {
            Adapter = adapter;
            Model = new PlotModel { Title = "Closing Report" };
            Model.LegendPlacement = LegendPlacement.Outside;
            Model.LegendPosition = LegendPosition.BottomRight;
            CategoryAxis = new OxyPlot.Axes.CategoryAxis() { Position = AxisPosition.Left };
            ValueAxis = new OxyPlot.Axes.LinearAxis()
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                MaximumPadding = 0.06,
                AbsoluteMinimum = 0
            };
        }

        private void Render()
        {
            foreach (OxyPlot.Series.BarSeries series in Adapter)
            {
                Model.Series.Add(series);
            }

            Model.Axes.Add(CategoryAxis);
            Model.Axes.Add(ValueAxis);
            rendered = true;
        }

        public void SaveToFile(string path)
        {
            if (!rendered)
            {
                Render();
            }

            var exporter = new PngExporter() { Width = 600, Height = 400 };
            var thread = new Thread(() =>
            {
                exporter.ExportToFile(Model, path);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }

    class LineChartView
    {
        private bool rendered = false;

        private PlotModel Model
        {
            get; set;
        }

        private List<OxyPlot.Series.LineSeries> Series
        {
            get; set;
        }

        private OxyPlot.Axes.LinearAxis FrequencyAxis
        {
            get; set;
        }

        private OxyPlot.Axes.TimeSpanAxis TimeAxis
        {
            get; set;
        }

        public LineChartView()
        {
            Model = new PlotModel() { Title = "Calls by Time" };
            Series = new List<OxyPlot.Series.LineSeries>();
            Model.LegendPlacement = LegendPlacement.Outside;
            Model.LegendPosition = LegendPosition.TopRight;
            FrequencyAxis = new OxyPlot.Axes.LinearAxis() { Position = AxisPosition.Left };
            TimeAxis = new OxyPlot.Axes.TimeSpanAxis()
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                MaximumPadding = 0.06,
                AbsoluteMinimum = 0
            };
        }

        private void Render()
        {
            foreach (var series in Series)
            {
                Model.Series.Add(series);
            }
            Model.Axes.Add(TimeAxis);
            Model.Axes.Add(FrequencyAxis);
            rendered = true;
        }

        public void SaveToFile(string path)
        {
            if (!rendered)
            {
                Render();
            }

            var exporter = new PngExporter() { Width = 800, Height = 600 };
            var thread = new Thread(() =>
            {
                exporter.ExportToFile(Model, path);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }


    class HtmlView
    {
        private static string templatePath = @"View.template";
        private bool rendered = false;

        public string ResultString
        {
            get;
            private set;
        }

        private object Model
        {
            get; set;
        }

        public HtmlView()
        {
            var config = new TemplateServiceConfiguration();
            config.DisableTempFileLocking = false;
            Engine.Razor = RazorEngineService.Create(config);
        }

        public void AddAccounts(Accounts accounts)
        {
            Model = new
            {
                //Statistics = accounts.Statistics(),
                Totals = new
                {
                    TotalReceived = accounts.TotalCount,
                    Inbound = accounts.InboundCount,
                    Outbound = accounts.OutboundCount,
                    AbandonRate = accounts.AbandonedRate
                }
            };
        }

        private void Render()
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
                model: Model
            );

            rendered = true;
        }

        public void SaveToFile(string path)
        {
            if (!rendered)
            {
                Render();
            }

            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(ResultString);
            }
        }
    }
}