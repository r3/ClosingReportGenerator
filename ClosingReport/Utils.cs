using OxyPlot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace ClosingReport
{
    public static class TimeManagement
    {
        public const int HOURS_IN_DAY = 24;
        public const int MINUTES_IN_HOUR = 60;
        public const int SECONDS_IN_MINUTE = 60;
        public const int MILLISECONDS_IN_SECOND = 1000;

        private static int? increment = null;
        private static TimeSpan? openingTime = null;
        private static TimeSpan? closingTime = null;

        public static int Increment
        {
            get
            {
                if (increment != null)
                {
                    return (int)increment;
                }

                string unparsed = ConfigurationManager.AppSettings["TimeIncrement"];
                int parsed;

                try
                {
                    parsed = Convert.ToInt32(unparsed);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"TimeIncrement of '{unparsed}' is not valid. Should be an integer. Got error: {e.Message}");
                }

                if (parsed % 5 != 0 || parsed < 5)
                {
                    throw new ArgumentException($"TimeIncrement is not a multiple of five, or is lower than five.");
                }

                increment = parsed;
                return parsed;
            }
        }

        public static TimeSpan OpeningTime
        {
            get
            {
                if (openingTime == null)
                {
                    openingTime = GetOperationTimes("OpeningTime");
                }

                return (TimeSpan)openingTime;
            }
        }

        public static TimeSpan ClosingTime
        {
            get
            {
                if (closingTime == null)
                {
                    closingTime = GetOperationTimes("ClosingTime");
                }

                return (TimeSpan)closingTime;
            }
        }

        private static TimeSpan GetOperationTimes(string name)
        {
            string unparsed;
            try
            {
                unparsed = ConfigurationManager.AppSettings[name];
            }
            catch (Exception)
            {
                throw new ArgumentException($"Unable to read value with key, '{name}' from the configuration file");
            }

            try
            {
                DateTime parsed = DateTime.Parse(unparsed);
                TimeSpan time = parsed.TimeOfDay;
                return time;
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to convert '{unparsed}' to TimeSpan. Please use format, 'HH:MM AM/PM' in config file. Got error: {e.Message}");
            }
        }

        public static TimeSpan NearestIncrement(DateTime time)
        {
            int minutesToNearestIncrement = (time.Minute / Increment) * Increment;
            return new TimeSpan(
                hours: time.Hour,
                minutes: minutesToNearestIncrement,
                seconds: 0
            );
        }

        public static int TimeDivRem(int days, int divisor, int conversionFactor, out int hourRemainder)
        {
            int remainder;
            int quotient = Math.DivRem(days, divisor, out remainder);
            hourRemainder = remainder * conversionFactor;
            return quotient;
        }

        public static TimeSpan AverageTime(IEnumerable<TimeSpan> times)
        {
            int collectionCount = 0;
            TimeSpan totalTime = new TimeSpan(0);
            foreach (TimeSpan time in times)
            {
                totalTime += time;
                collectionCount++;
            }
            
            if (collectionCount == 0)
            {
                ReportRunner.log.TraceEvent(TraceEventType.Warning, 1, $"Unable to compute average, no TimeSpan objects in enumerable");
                return new TimeSpan(0);
            }

            int hoursLeft;
            int avgDays = TimeDivRem(totalTime.Days, collectionCount, HOURS_IN_DAY, out hoursLeft);
            int minutesLeft;
            int avgHours = TimeDivRem(totalTime.Hours + hoursLeft, collectionCount, MINUTES_IN_HOUR, out minutesLeft);
            int secondsLeft;
            int avgMinutes = TimeDivRem(totalTime.Minutes + minutesLeft, collectionCount, SECONDS_IN_MINUTE, out secondsLeft);
            int millisecondsLeft;
            int avgSeconds = TimeDivRem(totalTime.Seconds + secondsLeft, collectionCount, MILLISECONDS_IN_SECOND, out millisecondsLeft);
            int avgMilliseconds = (totalTime.Milliseconds + millisecondsLeft) / collectionCount;
            
            return new TimeSpan(days: avgDays, hours: avgHours, minutes: avgMinutes, seconds: avgSeconds);
        }

        public static TimeSpan StampToSpan(string timestamp)
        {
            string[] timeParts = timestamp.Split(':');
            int hrs, mins, secs;

            try
            {
                hrs = int.Parse(timeParts[0]);
                mins = int.Parse(timeParts[1]);
                secs = int.Parse(timeParts[2]);
            }
            catch (Exception e)
            {
                throw new ParseException($"Failed to parse '{timestamp}' to TimeSpan. Got error: {e.Message}");
            }

            return new TimeSpan(hrs, mins, secs);
        }
    }

    public class TimeTracker : IEnumerable<KeyValuePair<TimeSpan, int>>
    {
        private SortedDictionary<TimeSpan, int> counts = new SortedDictionary<TimeSpan, int>();
        public Func<ICommunication, bool> IsTrackable;

        public int Count
        {
            get
            {
                return counts.Values.Sum();
            }
        }

        public string Name
        {
            get; private set;
        }

        public TimeTracker(string name, Func<ICommunication, bool> trackingCondition)
        {
            Name = name;
            IsTrackable = trackingCondition;
        }

        public void AddTime(DateTime time)
        {
            TimeSpan rounded = TimeManagement.NearestIncrement(time);

            if (rounded < TimeManagement.OpeningTime || rounded > TimeManagement.ClosingTime)
            {
                throw new ArgumentException($"Encountered time outside of opening ({TimeManagement.OpeningTime}) and closing ({TimeManagement.ClosingTime}) time: {time}");
            }

            counts[rounded]++;
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Added time, {time} to tracking as {rounded}. Count is now {counts[rounded]}.");
        }

        public bool TrackIfSupported(ICommunication comm)
        {
            if (IsTrackable(comm))
            {
                TimeSpan rounded = TimeManagement.NearestIncrement(comm.TimeOfReceipt);
                counts[rounded]++;
                return true;
            }

            return false;
        }

        public IEnumerator<KeyValuePair<TimeSpan, int>> GetEnumerator()
        {
            return counts.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return counts.GetEnumerator();
        }
    }

    public static class CommunicationFactories
    {
        public static ICommunication fromInboundRecord(string[] row)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(row[0]);
                string telephoneNumber = row[1];
                TimeSpan callDuration = TimeManagement.StampToSpan(row[2]);

                int agentId = ReportRunner.sentinel;
                int.TryParse(row[3], out agentId);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(row[4], out accountCode);

                TimeSpan ringDuration = TimeManagement.StampToSpan(row[5]);

                Communication comm = new Communication(firstRingTime, accountCode, CommDirection.Inbound, true, ringDuration, callDuration);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse communication from CVS row: {row}. Got error: {e.Message}");
            }
        }

        public static ICommunication fromOutboundRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);
                string telephoneNumber = record[1];
                TimeSpan callDuration = TimeManagement.StampToSpan(record[2]);

                int agentId = ReportRunner.sentinel;
                int.TryParse(record[3], out agentId);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(record[4], out accountCode);

                ICommunication comm = new Communication(firstRingTime, accountCode, CommDirection.Outbound, true, null, callDuration);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }

        public static ICommunication fromAbandonedRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(record[1], out accountCode);

                TimeSpan callDuration = TimeManagement.StampToSpan(record[2]);
                ICommunication comm = new Communication(firstRingTime, accountCode, CommDirection.Inbound, false, callDuration);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }
    }

    public class AccountsConfiguration : ConfigurationSection
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propAccounts;

        static AccountsConfiguration()
        {
            propAccounts = new ConfigurationProperty(null,
                                                    typeof(AccountsElementCollection),
                                                    null,
                                                    ConfigurationPropertyOptions.IsDefaultCollection);
            properties = new ConfigurationPropertyCollection { propAccounts };
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public AccountsElementCollection Accounts
        {
            get
            {
                return this[propAccounts] as AccountsElementCollection;
            }
        }
    }

    public class AccountsElementCollection : ConfigurationElementCollection
    {
        public AccountsElementCollection()
        {
            properties = new ConfigurationPropertyCollection();
        }

        private static ConfigurationPropertyCollection properties;

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "account";
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new AccountsElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as AccountsElement;
            if (elm == null) throw new ArgumentNullException();
            return elm.AccountName;
        }
    }

    public class AccountsElement : ConfigurationElement
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propAccount;
        private static ConfigurationProperty propCodes;

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public AccountsElement()
        {
            propAccount = new ConfigurationProperty("name", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            propCodes = new ConfigurationProperty("codes", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            properties = new ConfigurationPropertyCollection { propAccount, propCodes };
        }

        public AccountsElement(string accountName)
            : this()
        {
            AccountName = accountName;
        }

        public string AccountName
        {
            get
            {
                return this[propAccount] as string;
            }
            set
            {
                this[propAccount] = value;
            }
        }

        public int[] AccountCodes
        {
            get
            {
                string codes = this[propCodes] as string;
                try
                {
                    return codes.Split(',').Select(x => Convert.ToInt32(x)).ToArray();
                }
                catch (FormatException)
                {
                    ReportRunner.log.TraceEvent(TraceEventType.Warning, 1, $"Unable to parse codes from '{codes}', using: {ReportRunner.sentinel}");
                    return new int[] { ReportRunner.sentinel };
                }
            }
        }
    }
}
