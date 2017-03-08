using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace ClosingReport
{
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

                int agentId = ClosingReport.ReportRunner.sentinel;
                int.TryParse(row[3], out agentId);

                int accountCode = ClosingReport.ReportRunner.sentinel;
                int.TryParse(row[4], out accountCode);

                TimeSpan ringDuration = stampToSpan(row[5]);

                InboundCall call = new InboundCall(firstRingTime, accountCode, callDuration, telephoneNumber, agentId, ringDuration);
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

                int agentId = ClosingReport.ReportRunner.sentinel;
                int.TryParse(record[3], out agentId);

                int accountCode = ClosingReport.ReportRunner.sentinel;
                int.TryParse(record[4], out accountCode);

                OutboundCall call = new OutboundCall(firstRingTime, accountCode, callDuration, telephoneNumber, agentId);
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

                int accountCode = ClosingReport.ReportRunner.sentinel;
                int.TryParse(record[1], out accountCode);

                TimeSpan callDuration = stampToSpan(record[2]);
                AbandonedCall call = new AbandonedCall(firstRingTime, accountCode, callDuration);
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
        private SortedList inbound;
        private SortedList outbound;
        private SortedList abandon;

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

        public Account(string name, int[] codes)
        {
            this.name = name;
            this.codes = codes;
            this.inbound = new SortedList();
            this.outbound = new SortedList();
            this.abandon = new SortedList();
        }

        public void AddCall(InboundCall call)
        {
            inbound.Add(call, null);
        }

        public void AddCall(OutboundCall call)
        {
            outbound.Add(call, null);
        }

        public void AddCall(AbandonedCall call)
        {
            abandon.Add(call, null);
        }
    }

    class Accounts
    {
        private Dictionary<int, Account> accounts;
        private int sentinel;

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
                    }
                    else
                    {
                        throw new ArgumentException($"Could not add {account.Name} to accounts with code, {code}. Account code already used by {accounts[code].Name}");
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
                // Call already exists in that account and call type
            }
            catch (Exception)
            {
                // Otherwise failed to add call
            }
        }
    }
}
