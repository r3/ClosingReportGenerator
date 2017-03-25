using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace ClosingReport
{
    public class TimeManagement
    {
        private static int? increment = null;
        private static TimeSpan openingTime;
        private static TimeSpan closingTime;

        private Dictionary<TimeSpan, int> count;

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

                if (increment % 5 != 0 || increment < 5)
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

                return openingTime;
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

                return closingTime;
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
                return TimeSpan.Parse(unparsed);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to convert '{unparsed}' to TimeSpan. Got error: {e.Message}");
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

        public TimeManagement()
        {
            TimeSpan index = OpeningTime;
            TimeSpan incrementAsSpan = new TimeSpan(hours: 0, minutes: Increment, seconds: 0);

            while (index < ClosingTime)
            {
                count[index] = 0;
                index += incrementAsSpan;
                ReportRunner.log.TraceEvent(TraceEventType.Critical, 0, $"THING: Adding TimeSpan: {index}");
            }
        }

        public void AddTimes(IEnumerable<DateTime> times)
        {
            foreach (var time in times)
            {
                TimeSpan rounded = NearestIncrement(time);

                if (rounded < OpeningTime || rounded > ClosingTime)
                {
                    throw new ArgumentException($"Encountered time outside of opening ({OpeningTime}) and closing ({ClosingTime}) time: {time}");
                }
                count[rounded]++;
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
