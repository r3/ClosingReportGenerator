using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ClosingReport
{
    class ModelChartAdapter<TModel, TSeries> : IEnumerable<TSeries>
        where TSeries : Series
    {
        protected TModel Model
        {
            get;
            set;
        }

        public Func<TModel, IEnumerable<TSeries>> MakeSeries
        {
            get;
            set;
        }

        public ModelChartAdapter(TModel model, Func<TModel, IEnumerable<TSeries>> seriesConstructor)
        {
            Model = model;
            MakeSeries = seriesConstructor;
        }

        public IEnumerator<TSeries> GetEnumerator()
        {
            foreach (var series in MakeSeries(Model))
            {
                yield return series;
            }

            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class AccountsBarChartAdapter : ModelChartAdapter<Accounts, BarSeries>
    {
        public IEnumerable<string> Labels
        {
            get
            {
                return Model.Select((x) => x.Name);
            }
        }

        public AccountsBarChartAdapter(Accounts model, Func<Accounts, IEnumerable<BarSeries>> seriesConstructor)
            : base(model, seriesConstructor)
        {
        }

        public static IEnumerable<BarSeries> SeriesCtor(Accounts accounts)
        {
            var inbound = new BarSeries { Title = "Inbound", StrokeColor = OxyColors.Black, StrokeThickness = 1 };
            var outbound = new BarSeries { Title = "Outbound", StrokeColor = OxyColors.Black, StrokeThickness = 1 };
            var abandoned = new BarSeries { Title = "Abandoned", StrokeColor = OxyColors.Black, StrokeThickness = 1 };

            foreach (Account account in accounts)
            {
                inbound.Items.Add(new BarItem() { Value = account.TotalInbound });
                outbound.Items.Add(new BarItem() { Value = account.TotalOutbound });
                abandoned.Items.Add(new BarItem() { Value = account.TotalAbandoned });
            }

            yield return inbound;
            yield return outbound;
            yield return abandoned;
            yield break;
        }
    }

    class TimeTrackerLineChartAdapter : ModelChartAdapter<TimeTracker, LineSeries>
    {
        protected new IEnumerable<TimeTracker> Model
        {
            get;
            set;
        }

        public new Func<IEnumerable<TimeTracker>, IEnumerable<LineSeries>> MakeSeries
        {
            get;
            set;
        }

        public TimeTrackerLineChartAdapter(IEnumerable<TimeTracker> model, Func<IEnumerable<TimeTracker>, IEnumerable<LineSeries>> seriesConstructor)
            : base(null, null)
        {
            Model = model;
            MakeSeries = seriesConstructor;
        }

        private static LineSeries MakeInboundSeries()
        {
            return new LineSeries()
            {
                Title = "Inbound",
                Color = OxyColors.SkyBlue,
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerStroke = OxyColors.White,
                MarkerFill = OxyColors.SkyBlue,
                MarkerStrokeThickness = 1.5
            };
        }

        private static LineSeries MakeOutboundSeries()
        {
            return new LineSeries()
            {
                Title = "Outbound",
                Color = OxyColors.LawnGreen,
                MarkerType = MarkerType.Square,
                MarkerSize = 6,
                MarkerStroke = OxyColors.White,
                MarkerFill = OxyColors.LawnGreen,
                MarkerStrokeThickness = 1.5
            };
        }

        private static LineSeries MakeAbandonedSeries()
        {
            return new LineSeries()
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

        private static LineSeries SeriesFromTracker(TimeTracker tracker)
        {
                switch (tracker.Name)
                {
                    case "Inbound":
                        return MakeInboundSeries();
                    case "Outbound":
                        return MakeOutboundSeries();
                    default:
                        return MakeAbandonedSeries();
                }
        }

        public static IEnumerable<LineSeries> SeriesCtor(IEnumerable<TimeTracker> trackers)
        {
            foreach (TimeTracker tracker in trackers)
            {
                LineSeries series = SeriesFromTracker(tracker);
                foreach (KeyValuePair<TimeSpan, int> pair in tracker)
                {
                    series.Points.Add(new DataPoint(TimeSpanAxis.ToDouble(pair.Key), pair.Value));
                }

                yield return series;
            }
            yield break;
        }
    }
}
