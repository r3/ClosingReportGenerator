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

    public enum CommDirection
    {
        Inbound,
        Outbound
    }

    public interface ICommunication
    {
        CommDirection Direction
        {
            get;
        }
        
        bool WasReceived
        {
            get;
        }

        DateTime TimeOfReceipt
        {
            get;
        }

        object GroupId
        {
            get;
        }
    }

    public class Communication : ICommunication
    {

        public DateTime TimeOfReceipt
        {
            get;
        }

        public object GroupId
        {
            get;
        }

        public CommDirection Direction
        {
            get;
        }

        public bool WasReceived
        {
            get;
        }

        public override string ToString()
        {
            return $"Communication(TimeOfReceipt: {TimeOfReceipt}, GroupId: {GroupId}, Direction: {Direction}, WasReceived: {WasReceived})";
        }

        public Communication(DateTime timeOfReceipt, object groupId, CommDirection direction, bool wasReceived)
        {
            TimeOfReceipt = timeOfReceipt;
            GroupId = groupId;
            Direction = direction;
            WasReceived = wasReceived;
        }
    }

    public class Account
    {
        private List<ICommunication> communications = new List<ICommunication>();
        private List<TimeTracker> timeTrackers = new List<TimeTracker>();

        public string Name
        {
            get; set;
        }

        public int[] GroupIds // GroupId
        {
            get; set;
        }

        public int TotalInbound
        {
            get
            {
                return communications.Where(comm => comm.Direction == CommDirection.Inbound).Count();
            }
        }

        public int TotalOutbound
        {
            get
            {
                return communications.Where(comm => comm.Direction == CommDirection.Outbound).Count();
            }
        }

        public int TotalAbandoned
        {
            get
            {
                return communications.Where(comm => comm.Direction == CommDirection.Inbound && comm.WasReceived == false).Count();
            }
        }

        public Account(string name, int[] groupIds)
        {
            Name = name;
            GroupIds = groupIds;
        }

        public void AddCommunication(ICommunication comm)
        {
            communications.Add(comm);
            foreach (var tracker in timeTrackers)
            {
                try
                {
                    tracker.TrackIfSupported(comm);
                }
                catch (Exception e)
                {
                    ReportRunner.log.TraceEvent(TraceEventType.Warning, 1, $"Encountered an error trying to add '{comm}' to tracker: {e.Message}");
                }
            }
        }

        public void RegisterTrackers(IEnumerable<TimeTracker> trackers)
        {
            timeTrackers.AddRange(trackers);
        }
    }

    public class Accounts : IEnumerable<Account>
    {
        private int sentinel;
        private List<TimeTracker> trackers;
        private Dictionary<object, Account> accounts = new Dictionary<object, Account>();
        
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

        public Accounts(int sentinel, List<TimeTracker> trackers)
        {
            this.sentinel = sentinel;
            this.trackers = trackers;

            var cfg = ConfigurationManager.GetSection("accounts") as AccountsConfiguration;
            foreach (AccountsElement elem in cfg.Accounts)
            {
                var account = new Account(elem.AccountName, elem.AccountCodes);
                account.RegisterTrackers(trackers);

                foreach (var code in elem.AccountCodes)
                {
                    if (!accounts.Keys.Contains(code))
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

        public void AddCommunication(ICommunication comm)
        {
            Account account;
            try
            {
                account = accounts[comm.GroupId];
            }
            catch (KeyNotFoundException)
            {
                account = accounts[sentinel];
            }

            try
            {
                account.AddCommunication(comm);
            }
            catch (Exception e)
            {
                ReportRunner.log.TraceEvent(TraceEventType.Error, 1, $"Unable to add call, {comm}, got error: {e.Message}");
            }
        }

        IEnumerator<Account> IEnumerable<Account>.GetEnumerator()
        {
            return accounts.Values.Distinct<Account>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return accounts.Values.Distinct().GetEnumerator();
        }
    }
}