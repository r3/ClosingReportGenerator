using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ClosingReport
{
    struct Stats
    {
        public string AccountName;
        public TimeSpan InboundAverage;
        public TimeSpan AbandonedAverage;
        public int TotalInbound;
        public int TotalOutbound;
        public int TotalAbandoned;
    }

    class ModelChartAdapter<TModel, TSeries> : IEnumerable<TSeries>
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

    class AccountsHtmlAdapter : ModelChartAdapter<Accounts, Stats>
    {
        public int TotalCount
        {
            get
            {
                return InboundCount + Model["Abandoned"].Count;
            }
        }

        public int InboundCount
        {
            get
            {
                return Model["Inbound"].Count;
            }
        }

        public int OutboundCount
        {
            get
            {
                return Model["Outbound"].Count;
            }
        }

        public float AbandonedRate
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public AccountsHtmlAdapter(Accounts model, Func<Accounts, IEnumerable<Stats>> seriesConstructor)
            : base(model, seriesConstructor)
        {
        }

        public static IEnumerable<Stats> SeriesCtor(Accounts accounts)
        {
            throw new NotImplementedException();
            foreach (Account account in accounts)
            {
                yield return new Stats
                {
                    // TODO: Fix these;
                    AccountName = account.Name,
                    InboundAverage = new TimeSpan(),
                    AbandonedAverage = new TimeSpan(),
                    TotalInbound = 0,
                    TotalOutbound = 0,
                    TotalAbandoned = 0 
                };
            }

            yield break;
        }
    }

    class AccountsBarChartAdapter : ModelChartAdapter<Accounts, BarSeries>
    {
        public IEnumerable<string> Labels
        {
            get
            {
                return new List<string>() { "" };
                //return Model.Select((x) => x.Name);
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
                // TODO: FIX DIS
                throw new NotImplementedException();
                inbound.Items.Add(new BarItem() { Value = 0 });
                outbound.Items.Add(new BarItem() { Value = 0 });
                abandoned.Items.Add(new BarItem() { Value = 0 });
            }

            yield return inbound;
            yield return outbound;
            yield return abandoned;
            yield break;
        }
    }

    class TimeTrackersLineChartAdapter : ModelChartAdapter<IEnumerable<TimeTracker>, LineSeries>
    {
        public TimeTrackersLineChartAdapter(IEnumerable<TimeTracker> model, Func<IEnumerable<TimeTracker>, IEnumerable<LineSeries>> seriesConstructor)
            : base(model, seriesConstructor)
        {
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
