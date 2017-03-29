using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace ClosingReport
{
    public class TimeManagement : IEnumerable<KeyValuePair<TimeSpan, int>>
    {
        public const int HOURS_IN_DAY = 24;
        public const int MINUTES_IN_HOUR = 60;
        public const int SECONDS_IN_MINUTE = 60;
        public const int MILLISECONDS_IN_SECOND = 1000;

        private static int? increment = null;
        private static TimeSpan? openingTime = null;
        private static TimeSpan? closingTime = null;

        private SortedDictionary<TimeSpan, int> count;

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
            int minutesToNearestIncrement = time.Minute / Increment;
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

        public TimeManagement()
        {
            TimeSpan index = OpeningTime;
            TimeSpan incrementAsSpan = new TimeSpan(hours: 0, minutes: Increment, seconds: 0);

            count = new SortedDictionary<TimeSpan, int>();
            while (index < ClosingTime)
            {
                count[index] = 0;
                index += incrementAsSpan;
            }
        }

        public void AddTime(DateTime time)
        {
            ReportRunner.log.TraceEvent(TraceEventType.Critical, 0, $"Adding time, {time} to tracking.");
            TimeSpan rounded = NearestIncrement(time);

            // TODO:
            // Parse Open and ClosingTime as a DateTime, and then check for inclusion prior to rounding to increment
            // Perhaps just manage this check when adding the call instead, or in both locations, I guess.
            if (rounded < OpeningTime || rounded > ClosingTime)
            {
                throw new ArgumentException($"Encountered time outside of opening ({OpeningTime}) and closing ({ClosingTime}) time: {time}");
            }
            ReportRunner.log.TraceEvent(TraceEventType.Critical, 0, $"Rounded: {rounded}");
            count[rounded]++;

        }

        public IEnumerator<KeyValuePair<TimeSpan, int>> GetEnumerator()
        {
            return count.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return count.GetEnumerator();
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
