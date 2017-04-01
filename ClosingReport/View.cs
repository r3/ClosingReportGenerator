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
    interface IView
    {
        void AddAccounts(Accounts accounts);
        void SaveToFile(string path);       
    }

    class BarChartView : IView
    {
        private bool rendered = false;

        private PlotModel Model
        {
            get; set;
        }

        private OxyPlot.Series.BarSeries InboundSeries
        {
            get; set;
        }

        private OxyPlot.Series.BarSeries OutboundSeries
        {
            get; set;
        }

        private OxyPlot.Series.BarSeries AbandonedSeries
        {
            get; set;
        }

        private OxyPlot.Axes.CategoryAxis CategoryAxis
        {
            get; set;
        }

        private OxyPlot.Axes.LinearAxis ValueAxis
        {
            get; set;
        }

        public BarChartView()
        {
            Model = new PlotModel { Title = "Closing Report" };
            Model.LegendPlacement = LegendPlacement.Outside;
            Model.LegendPosition = LegendPosition.BottomRight;
            InboundSeries = new OxyPlot.Series.BarSeries { Title = "Inbound", StrokeColor = OxyColors.Black, StrokeThickness = 1 };
            OutboundSeries = new OxyPlot.Series.BarSeries { Title = "Outbound", StrokeColor = OxyColors.Black, StrokeThickness = 1 };
            AbandonedSeries = new OxyPlot.Series.BarSeries { Title = "Abandoned", StrokeColor = OxyColors.Black, StrokeThickness = 1 };
            CategoryAxis = new OxyPlot.Axes.CategoryAxis() { Position = AxisPosition.Left };
            ValueAxis = new OxyPlot.Axes.LinearAxis()
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                MaximumPadding = 0.06,
                AbsoluteMinimum = 0
            };
        }

        public void AddAccounts(Accounts accounts)
        {

            foreach (Account account in accounts)
            {
                InboundSeries.Items.Add(new BarItem() { Value = account.TotalInbound });
                OutboundSeries.Items.Add(new BarItem() { Value = account.TotalOutbound });
                AbandonedSeries.Items.Add(new BarItem() { Value = account.TotalAbandoned });
                CategoryAxis.Labels.Add(account.Name);
            }
        }

        private void Render()
        {
            Model.Series.Add(InboundSeries);
            Model.Series.Add(OutboundSeries);
            Model.Series.Add(AbandonedSeries);
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

    class LineChartView : IView
    {
        private bool rendered = false;

        private PlotModel Model
        {
            get; set;
        }

        private OxyPlot.Series.LineSeries InboundSeries
        {
            get; set;
        }

        private OxyPlot.Series.LineSeries OutboundSeries
        {
            get; set;
        }

        private OxyPlot.Series.LineSeries AbandonedSeries
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

            InboundSeries = new OxyPlot.Series.LineSeries()
            {
                Title = "Inbound",
                Color = OxyColors.SkyBlue,
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerStroke = OxyColors.White,
                MarkerFill = OxyColors.SkyBlue,
                MarkerStrokeThickness = 1.5
            };
            OutboundSeries = new OxyPlot.Series.LineSeries()
            {
                Title = "Outbound",
                Color = OxyColors.LawnGreen,
                MarkerType = MarkerType.Square,
                MarkerSize = 6,
                MarkerStroke = OxyColors.White,
                MarkerFill = OxyColors.LawnGreen,
                MarkerStrokeThickness = 1.5
            };
            AbandonedSeries = new OxyPlot.Series.LineSeries()
            {
                Title = "Abandoned",
                Color = OxyColors.OrangeRed,
                MarkerType = MarkerType.Cross,
                MarkerSize = 6,
                MarkerStroke = OxyColors.White,
                MarkerFill = OxyColors.OrangeRed,
                MarkerStrokeThickness = 1.5
            };

        }

        public void AddAccounts(Accounts accounts)
        {
            foreach (KeyValuePair<TimeSpan, int> pair in accounts.InboundTimes)
            {
                InboundSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
            }
            foreach (KeyValuePair<TimeSpan, int> pair in accounts.OutboundTimes)
            {
                OutboundSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
            }
            foreach (KeyValuePair<TimeSpan, int> pair in accounts.AbandonedTimes)
            {
                AbandonedSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
            }
        }

        private void Render()
        {
            Model.Series.Add(InboundSeries);
            Model.Series.Add(OutboundSeries);
            Model.Series.Add(AbandonedSeries);
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


    class HtmlView : IView
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
                Statistics = accounts.Statistics(),
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