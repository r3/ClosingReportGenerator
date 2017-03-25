using OxyPlot;
using OxyPlot.Series;
using System.Collections.Generic;

namespace ClosingReport
{
    class ChartView
    {
        public PlotModel Model
        {
            get;
            private set;
        }

        public ChartView(IEnumerable<IDataPointProvider> dataToPlot)
        {
            Model = new PlotModel { Title = "Closing Report" };
            Model.Series.Add(dataToPlot);
        }
    }
}
