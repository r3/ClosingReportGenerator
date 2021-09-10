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
                return InboundCount + Model.Trackers["Abandoned"].Count;
            }
        }

        public int InboundCount
        {
            get
            {
                return Model.Trackers["Inbound"].Count;
            }
        }

        public int OutboundCount
        {
            get
            {
                return Model.Trackers["Outbound"].Count;
            }
        }

        public int AbandonedCount
        {
            get
            {
                return Model.Trackers["Abandoned"].Count;
            }
        }

        public float AbandonedRate
        {
            get
            {
                try
                {
                    return AbandonedCount / (float)TotalCount;
                }
                catch (DivideByZeroException)
                {
                    return 0;
                }
            }
        }

        private int UnrecognizedCode
        {
            get; set;
        }

        public List<ICommunication> Unrecognized
        {
            get
            {
                return new List<ICommunication>(from x in Model.GetAccount(UnrecognizedCode) select x);
            }
        }

        public AccountsHtmlAdapter(Accounts model, Func<Accounts, IEnumerable<Stats>> seriesConstructor, int unrecognizedCode)
            : base(model, seriesConstructor)
        {
            UnrecognizedCode = unrecognizedCode;
        }

        public static IEnumerable<Stats> SeriesCtor(Accounts accounts)
        {
            var IsInbound = accounts.Trackers["Inbound"].IsTrackable;
            var IsOutbound = accounts.Trackers["Outbound"].IsTrackable;
            var IsAbandoned = accounts.Trackers["Abandoned"].IsTrackable;

            foreach (Account account in accounts)
            {
                var abandons = new List<ICommunication>(from x in account where IsAbandoned(x) select x);
                var inbounds = new List<ICommunication>(from x in account where IsInbound(x) select x);
                var outbounds = new List<ICommunication>(from x in account where IsOutbound(x) select x);

                yield return new Stats
                {
                    AccountName = account.Name,
                    InboundAverage = TimeManagement.AverageTime(from comm in inbounds select comm.TimeSpentPending),
                    AbandonedAverage = TimeManagement.AverageTime(from comm in abandons select comm.Duration),
                    TotalInbound = inbounds.Count,
                    TotalOutbound = outbounds.Count,
                    TotalAbandoned = abandons.Count
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
                return from Account account in Model select account.Name;
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
                var IsInbound = accounts.Trackers["Inbound"].IsTrackable;
                var IsOutbound = accounts.Trackers["Outbound"].IsTrackable;
                var IsAbandoned = accounts.Trackers["Abandoned"].IsTrackable;

                int inboundCount = (from comm in account where IsInbound(comm) select comm).Count();
                int outboundCount = (from comm in account where IsOutbound(comm) select comm).Count();
                int abandonedCount = (from comm in account where IsAbandoned(comm) select comm).Count();

                inbound.Items.Add(new BarItem() { Value = inboundCount });
                outbound.Items.Add(new BarItem() { Value = outboundCount });
                abandoned.Items.Add(new BarItem() { Value = abandonedCount });
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
