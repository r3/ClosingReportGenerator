using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Diagnostics;
using System.Collections;

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

    abstract class Call
    {
        public DateTime FirstRingTime
        {
            get; set;
        }

        public int AccountCode
        {
            get; set;
        }

        public TimeSpan CallDuration
        {
            get; set;
        }

        public string TelephoneNumber
        {
            get; set;
        }

        public int AgentId
        {
            get; set;
        }

        public TimeSpan RingDuration
        {
            get; set;
        }

        public override string ToString()
        {
            return $"Call(firstRingTime: {FirstRingTime}, accountCode: {AccountCode}, callDuration: {CallDuration})";
        }

        protected static TimeSpan stampToSpan(string timestamp)
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

        public Call(DateTime firstRingTime, int accountCode, TimeSpan callDuration)
        {
            FirstRingTime = firstRingTime;
            AccountCode = accountCode;
            CallDuration = callDuration;
        }
    }

    class InboundCall : Call
    {
        public InboundCall(DateTime firstRingTime, int accountCode, TimeSpan callDuration, string telephoneNumber, int agentId, TimeSpan ringDuration)
            : base(firstRingTime, accountCode, callDuration)
        {
            TelephoneNumber = telephoneNumber;
            AgentId = agentId;
            RingDuration = ringDuration;
        }

        public static InboundCall fromRecord(string[] row)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(row[0]);
                string telephoneNumber = row[1];
                TimeSpan callDuration = stampToSpan(row[2]);

                int agentId = ReportRunner.sentinel;
                int.TryParse(row[3], out agentId);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(row[4], out accountCode);

                TimeSpan ringDuration = stampToSpan(row[5]);

                InboundCall call = new InboundCall(firstRingTime, accountCode, callDuration, telephoneNumber, agentId, ringDuration);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed call: {call}");
                return call;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {row}. Got error: {e.Message}");
            }
        }
    }

    class OutboundCall : Call
    {
        public OutboundCall(DateTime firstRingTime, int accountCode, TimeSpan callDuration, string telephoneNumber, int agentId)
            : base(firstRingTime, accountCode, callDuration)
        {
            TelephoneNumber = telephoneNumber;
            AgentId = agentId;
        }

        public static OutboundCall fromRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);
                string telephoneNumber = record[1];
                TimeSpan callDuration = stampToSpan(record[2]);

                int agentId = ReportRunner.sentinel;
                int.TryParse(record[3], out agentId);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(record[4], out accountCode);

                OutboundCall call = new OutboundCall(firstRingTime, accountCode, callDuration, telephoneNumber, agentId);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed call: {call}");
                return call;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }
    }

    class AbandonedCall : Call
    {
        public AbandonedCall(DateTime firstRingTime, int accountCode, TimeSpan callDuration)
            : base(firstRingTime, accountCode, callDuration)
        {
        }

        public static AbandonedCall fromRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);

                int accountCode = ReportRunner.sentinel;
                int.TryParse(record[1], out accountCode);

                TimeSpan callDuration = stampToSpan(record[2]);
                AbandonedCall call = new AbandonedCall(firstRingTime, accountCode, callDuration);
                ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Parsed call: {call}");
                return call;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }
    }

    class Account
    {
        private int[] codes;
        private List<InboundCall> inbound;
        private List<OutboundCall> outbound;
        private List<AbandonedCall> abandoned;
        private TimeManagement inboundTimes = null;
        private TimeManagement outboundTimes = null;
        private TimeManagement abandonedTimes = null;

        public string Name
        {
            get; set;
        }

        public int[] Codes
        {
            get; set;
        }

        public int TotalInbound
        {
            get
            {
                return inbound.Count;
            }
        }

        public int TotalOutbound
        {
            get
            {
                return outbound.Count;
            }
        }

        public int TotalAbandoned
        {
            get
            {
                return abandoned.Count;
            }
        }

        public TimeManagement InboundTimes
        {
            get
            {
                return inboundTimes;
            }
            set
            {
                inboundTimes = value;
            }
        }

        public TimeManagement OutboundTimes
        {
            get
            {
                return outboundTimes;
            }
            set
            {
                outboundTimes = value;
            }
        }

        public TimeManagement AbandonedTimes
        {
            get
            {
                return abandonedTimes;
            }
            set
            {
                abandonedTimes = value;
            }
        }

        public Account(string name, int[] codes)
        {
            Name = name;
            Codes = codes;

            inbound = new List<InboundCall>();
            outbound = new List<OutboundCall>();
            abandoned = new List<AbandonedCall>();
        }

        public void AddCall(InboundCall call)
        {
            inbound.Add(call);
            if (InboundTimes != null)
            {
                InboundTimes.AddTime(call.FirstRingTime);
            }
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s inbound: {call}");
        }

        public void AddCall(OutboundCall call)
        {
            outbound.Add(call);
            if (OutboundTimes != null)
            {
                OutboundTimes.AddTime(call.FirstRingTime);
            }
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s outbound: {call}");
        }

        public void AddCall(AbandonedCall call)
        {
            abandoned.Add(call);
            if (AbandonedTimes != null)
            {
                AbandonedTimes.AddTime(call.FirstRingTime);
            }
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s abandoned: {call}");
        }

        public Stats Statistics()
        {
            return new Stats()
            {
                AccountName = Name,
                InboundAverage = TimeManagement.AverageTime(from call in inbound select call.RingDuration),
                AbandonedAverage = TimeManagement.AverageTime(from call in abandoned select call.CallDuration),
                TotalInbound = TotalInbound,
                TotalOutbound = TotalOutbound,
                TotalAbandoned = TotalAbandoned
            };
        }
    }

    class Accounts : IEnumerable<Account>
    {
        private Dictionary<int, Account> accounts;
        private int sentinel;
        private bool? includeOthers = null;

        public int InboundCount
        {
            get
            {
                return accounts.Values.Select(x => x.TotalInbound).Sum();
            }
        }

        public int OutboundCount
        {
            get
            {
                return accounts.Values.Select(x => x.TotalOutbound).Sum();
            }
        }

        public int AbandonedCount
        {
            get
            {
                return accounts.Values.Select(x => x.TotalAbandoned).Sum();
            }
        }

        public int TotalCount
        {
            get
            {
                return InboundCount + AbandonedCount;
            }
        }

        public float AbandonedRate
        {
            get
            {
                return (TotalCount != 0) ? AbandonedCount / TotalCount : 0;
            }
        }

        public TimeManagement InboundTimes
        {
            get; private set;
        }

        public TimeManagement OutboundTimes
        {
            get; private set;
        }

        public TimeManagement AbandonedTimes
        {
            get; private set;
        }

        public bool IncludeOthers
        {
            get
            {
                if (includeOthers == null)
                {
                    string unparsed = ConfigurationManager.AppSettings["IncludeOthers"];
                    try
                    {
                        includeOthers = bool.Parse(unparsed);
                    }
                    catch (ArgumentException)
                    {
                        ReportRunner.log.TraceEvent(TraceEventType.Error, 1, $"Could not parse, 'IncludeOthers' from config (read: {unparsed}, defaulting to false");
                        includeOthers = false;
                    }
                }

                return (bool)includeOthers;
            }
        }

        public Accounts(int sentinel)
        {
            this.sentinel = sentinel;
            accounts = new Dictionary<int, Account>();
            InboundTimes = new TimeManagement();
            OutboundTimes = new TimeManagement();
            AbandonedTimes = new TimeManagement();

            var cfg = ConfigurationManager.GetSection("accounts") as AccountsConfiguration;
            foreach (AccountsElement elem in cfg.Accounts)
            {
                var account = new Account(elem.AccountName, elem.AccountCodes);


                if (account.Name == "Others" && IncludeOthers)
                {
                    account.InboundTimes = (IncludeOthers) ? InboundTimes : new TimeManagement();
                    account.OutboundTimes = (IncludeOthers) ? OutboundTimes : new TimeManagement();
                    account.AbandonedTimes = (IncludeOthers) ? AbandonedTimes : new TimeManagement();
                }
                else
                {
                    account.InboundTimes = InboundTimes;
                    account.OutboundTimes = OutboundTimes;
                    account.AbandonedTimes = AbandonedTimes;
                }

                foreach (var code in elem.AccountCodes)
                {
                    if (!accounts.Keys.Contains<int>(code))
                    {
                        accounts[code] = account;
                        ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding account, '{account.Name}' with code, '{code}'");
                    }
                    else
                    {
                        string err = $"Could not add {account.Name} to accounts with code, {code}. Account code already used by {accounts[code].Name}";
                        ReportRunner.log.TraceEvent(TraceEventType.Error, 1, err);
                        throw new ArgumentException(err);
                    }
                }
            }
        }

        public void AddCall<T>(T call) where T : Call
        {
            Account account;
            try
            {
                account = accounts[call.AccountCode];
            }
            catch (KeyNotFoundException)
            {
                account = accounts[sentinel];
            }

            try
            {
                account.AddCall((dynamic)call);
            }
            catch (ArgumentException)
            {
                ReportRunner.log.TraceEvent(TraceEventType.Warning, 1, $"Not adding duplicate call: {call}");
            }
            catch (Exception e)
            {
                ReportRunner.log.TraceEvent(TraceEventType.Error, 1, $"Unable to add call, {call}, got error: {e.Message}");
            }
        }

        public IEnumerable<Stats> Statistics()
        {
            foreach (Account account in accounts.Values.Distinct<Account>())
            {
                yield return account.Statistics();
            }
            yield break;
        }

        IEnumerator<Account> IEnumerable<Account>.GetEnumerator()
        {
            foreach (Account account in accounts.Values.Distinct<Account>())
            {
                yield return account;
            }
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (Account account in accounts.Values.Distinct<Account>())
            {
                yield return account;
            }
            yield break;
        }
    }
}