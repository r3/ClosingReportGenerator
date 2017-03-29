using System;
using System.Collections.Generic;
using System.IO;
using RazorEngine;
using RazorEngine.Templating;
using System.Configuration;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Threading;

namespace ClosingReport
{
    class BarChartView
    {
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

        public void Render()
        {
            Model.Series.Add(InboundSeries);
            Model.Series.Add(OutboundSeries);
            Model.Series.Add(AbandonedSeries);
            Model.Axes.Add(CategoryAxis);
            Model.Axes.Add(ValueAxis);
        }

        public void SaveToFile(string path)
        {
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
            foreach (Account account in accounts)
            {
                foreach (KeyValuePair<TimeSpan, int> pair in account.InboundTimes)
                {
                    InboundSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
                }
                foreach (KeyValuePair<TimeSpan, int> pair in account.OutboundTimes)
                {
                    OutboundSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
                }
                foreach (KeyValuePair<TimeSpan, int> pair in account.AbandonedTimes)
                {
                    AbandonedSeries.Points.Add(new DataPoint(OxyPlot.Axes.TimeSpanAxis.ToDouble(pair.Key), pair.Value));
                }
            }
        }

        public void Render()
        {
            Model.Series.Add(InboundSeries);
            Model.Series.Add(OutboundSeries);
            Model.Series.Add(AbandonedSeries);
            Model.Axes.Add(TimeAxis);
            Model.Axes.Add(FrequencyAxis);
        }

        public void SaveToFile(string path)
        {
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

        public void SaveToFile(string path)
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(ResultString);
            }
        }
    }
}