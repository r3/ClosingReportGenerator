﻿using System;
using System.Collections.Generic;
using System.IO;
using RazorEngine;
using RazorEngine.Templating;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Axes;
using System.Threading;
using RazorEngine.Configuration;
using System.Net.Mail;
using System.Net.Mime;
using System.Configuration;
using System.Xml;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ClosingReport
{
    class BarChartView
    {
        private bool rendered = false;

        private AccountsBarChartAdapter Adapter
        {
            get; set;
        }

        private PlotModel Model
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

        public BarChartView(AccountsBarChartAdapter adapter)
        {
            Adapter = adapter;
            Model = new PlotModel { Title = "Closing Report" };
            Model.LegendPlacement = LegendPlacement.Outside;
            Model.LegendPosition = LegendPosition.BottomRight;
            CategoryAxis = new OxyPlot.Axes.CategoryAxis() { Position = AxisPosition.Left };
            CategoryAxis.Labels.AddRange(Adapter.Labels);
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

        private TimeTrackersLineChartAdapter Adapter
        {
            get; set;
        }

        private PlotModel Model
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

        public LineChartView(TimeTrackersLineChartAdapter adapter)
        {
            Adapter = adapter;
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
        }

        private void Render()
        {
            foreach (OxyPlot.Series.LineSeries series in Adapter)
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

        public string AsCode
        {
            get;
            private set;
        }

        private AccountsHtmlAdapter Adapter
        {
            get; set;
        }

        private object Model
        {
            get; set;
        }

        private string Template
        {
            get; set;
        }

        public HtmlView(AccountsHtmlAdapter adapter)
        {
            Adapter = adapter;
            var config = new TemplateServiceConfiguration();
            config.DisableTempFileLocking = false;
            Engine.Razor = RazorEngineService.Create(config);
            Template = ReadTemplate();
        }

        private static string ReadTemplate()
        {
            if (!File.Exists(templatePath))
            {
                throw new ArgumentException($"Could not open the template file at, '{templatePath}'");
            }

            using (StreamReader reader = new StreamReader(templatePath))
            {
                return reader.ReadToEnd();
            }
        }

        private void Render()
        {
            int maxPerRow = 3;
            int.TryParse(ClosingReport.config.AppSettings.Settings["ViewMaxPerRow"].Value, out maxPerRow);

            AsCode = Engine.Razor.RunCompile(
                templateSource: Template,
                name: "ClosingReportKey",
                modelType: null,
                model: new
                {
                    Statistics = Adapter as IEnumerable<Stats>,
                    RowMax = maxPerRow,
                    Totals = new
                    {
                        Abandoned = Adapter.AbandonedCount,
                        Inbound = Adapter.InboundCount,
                        Outbound = Adapter.OutboundCount,
                        AbandonRate = Adapter.AbandonedRate.ToString("P1")
                    },
                    Unrecognized = Adapter.Unrecognized
                }
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
                writer.Write(AsCode);
            }
        }
    }

    class ViewMailer
    {
        private MailMessage message;
        private HtmlDocument document;
        private List<LinkedResource> linkedResources;

        public string AsCode
        {
            get
            {
                return document.DocumentNode.OuterHtml;
            }
        }

        private string parsedSubject
        {
            get
            {
                string unparsed = ClosingReport.config.AppSettings.Settings["Subject"].Value;
                string pattern = @"\{.*\}";
                Match match = Regex.Match(unparsed, pattern);
                
                if (!match.Success)
                {
                    return unparsed;
                }

                string dateString;
                switch (match.Value.ToLower())
                {
                    case "{day}":
                        dateString = DateTime.Now.ToString("d");
                        break;
                    case "{month}":
                        dateString = DateTime.Now.ToString("y");
                        break;
                    case "{lastmonth}":
                        var today = DateTime.Today;
                        var month = new DateTime(today.Year, today.Month, 1);
                        dateString = month.AddMonths(-1).ToString("y");
                        break;
                    default:
                        dateString = "";
                        ClosingReport.log.TraceEvent(System.Diagnostics.TraceEventType.Error, 2, $"Unable to parse subject variable, '{match.Value}'");
                        break;
                }
                return Regex.Replace(unparsed, pattern, dateString);
            }
        }
        
        public ViewMailer(string html)
        {
            document = new HtmlDocument();
            document.LoadHtml(html);

            linkedResources = new List<LinkedResource>();

            message = new MailMessage();
            message.IsBodyHtml = true;
            message.Subject = parsedSubject;
            message.From = new MailAddress(ClosingReport.config.AppSettings.Settings["FromEmailAddress"].Value);
            message.Sender = message.From;

            string destinations = ClosingReport.config.AppSettings.Settings["DestinationAddresses"].Value;
            foreach (var destination in destinations.Split(','))
            {
                if (destination.Trim() != "")
                {
                    message.To.Add(destination);
                }
            }
        }

        public void ImbedImageAtId(string nodeId, string imagePath)
        {
            LinkedResource resource = new LinkedResource(imagePath, MediaTypeNames.Image.Jpeg);
            resource.ContentId = Guid.NewGuid().ToString();

            HtmlNode node = document.GetElementbyId(nodeId);
            if (node == null)
            {
                throw new ArgumentException($"Id, '{nodeId}' not found in document");
            }
            node.Attributes["src"].Value = $"cid:{resource.ContentId}";
            linkedResources.Add(resource);
        }
        
        public void SendMail(SmtpClient client)
        {
            AlternateView view = AlternateView.CreateAlternateViewFromString(AsCode, null, MediaTypeNames.Text.Html);
            foreach (var linked in linkedResources)
            {
                view.LinkedResources.Add(linked);
            }
            message.AlternateViews.Add(view);
            client.Send(message);
        }
    }
}