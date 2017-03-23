using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Diagnostics;

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

    abstract class Call : IComparable
    {
        private DateTime firstRingTime;
        private int accountCode;
        private TimeSpan callDuration;
        private string telephoneNumber;
        private int agentId;
        private TimeSpan ringDuration;
        
        public Call(DateTime firstRingTime, int accountCode, TimeSpan callDuration)
        {
            this.firstRingTime = firstRingTime;
            this.accountCode = accountCode;
            this.callDuration = callDuration;
        }

        public DateTime FirstRingTime
        {
            get
            {
                return firstRingTime;
            }
        }

        public int AccountCode
        {
            get
            {
                return accountCode;
            }
        }

        public TimeSpan CallDuration
        {
            get
            {
                return callDuration;
            }
        }

        public string TelephoneNumber
        {
            get
            {
                return telephoneNumber;
            }
            set
            {
                telephoneNumber = value;
            }
        }

        public int AgentId
        {
            get
            {
                return agentId;
            }
            set
            {
                agentId = value;
            }
        }

        public TimeSpan RingDuration
        {
            get
            {
                return this.ringDuration;
            }
            set
            {
                this.ringDuration = value;
            }
        }

        public int CompareTo(Object other)
        {
            Call otherCall = other as Call;
            if (otherCall != null)
            {
                return firstRingTime.CompareTo(otherCall.FirstRingTime); ;
            }
            else
            {
                throw new ArgumentException("Object is not a Call");
            }
        }

        public override string ToString()
        {
            return $"Call(firstRingTime: {FirstRingTime}, accountCode: {AccountCode}, callDuration: {CallDuration})";
        }

        public static TimeSpan stampToSpan(string timestamp)
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
        private string name;
        private int[] codes;
        private List<InboundCall> inbound;
        private List<OutboundCall> outbound;
        private List<AbandonedCall> abandon;

        public string Name
        {
            get
            {
                return name;
            }
        }

        public int[] Codes
        {
            get
            {
                return codes;
            }
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
                return abandon.Count;
            }
        }

        public Account(string name, int[] codes)
        {
            this.name = name;
            this.codes = codes;
            this.inbound = new List<InboundCall>();
            this.outbound = new List<OutboundCall>();
            this.abandon = new List<AbandonedCall>();
        }

        public void AddCall(InboundCall call)
        {
            inbound.Add(call);
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s inbound: {call}");
        }

        public void AddCall(OutboundCall call)
        {
            outbound.Add(call);
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s outbound: {call}");
        }

        public void AddCall(AbandonedCall call)
        {
            abandon.Add(call);
            ReportRunner.log.TraceEvent(TraceEventType.Information, 0, $"Adding call to {Name}'s abandoned: {call}");
        }

        private TimeSpan AverageTime(IEnumerable<TimeSpan> times)
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
            
            int avgDays = totalTime.Days / collectionCount;
            int avgHours = totalTime.Hours / collectionCount;
            int avgMinutes = totalTime.Minutes / collectionCount;
            int avgSeconds = totalTime.Seconds / collectionCount;
            int avgMilliseconds = totalTime.Milliseconds / collectionCount;
            
            return new TimeSpan(days: avgDays, hours: avgHours, minutes: avgMinutes, seconds: avgSeconds);
        }

        public Stats Statistics()
        {
            return new Stats()
            {
                AccountName = Name,
                InboundAverage = AverageTime(from call in inbound select call.RingDuration),
                AbandonedAverage = AverageTime(from call in abandon select call.CallDuration),
                TotalInbound = TotalInbound,
                TotalOutbound = TotalOutbound,
                TotalAbandoned = TotalAbandoned
            };
        }
    }

    class Accounts
    {
        private Dictionary<int, Account> accounts;
        private int sentinel;

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
                return (AbandonedCount != 0) ? TotalCount / AbandonedCount : 0;
            }
        }

        public Accounts(int sentinel)
        {
            this.sentinel = sentinel;
            accounts = new Dictionary<int, Account>();

            var cfg = ConfigurationManager.GetSection("accounts") as AccountsConfiguration;
            foreach (AccountsElement elem in cfg.Accounts)
            {
                var account = new Account(elem.AccountName, elem.AccountCodes);

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

        public void AddCall<T>(int accountCode, T call) where T : Call
        {
            Account account;
            try
            {
                account = accounts[accountCode];
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
    }
}
