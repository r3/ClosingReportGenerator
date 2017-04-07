using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Diagnostics;
using System.Collections;

namespace ClosingReport
{
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

        TimeSpan TimeSpentPending
        {
            get;
        }

        TimeSpan Duration
        {
            get;
        }
    }

    public class Communication : ICommunication
    {

        public DateTime TimeOfReceipt
        {
            get; private set;
        }

        public object GroupId
        {
            get; private set;
        }

        public CommDirection Direction
        {
            get; private set;
        }

        public bool WasReceived
        {
            get; private set;
        }

        public TimeSpan TimeSpentPending
        {
            get; private set;
        }

        public TimeSpan Duration
        {
            get; private set;
        }

        public override string ToString()
        {
            return $"Communication(TimeOfReceipt: {TimeOfReceipt}, GroupId: {GroupId}, Direction: {Direction}, WasReceived: {WasReceived})";
        }

        public Communication(DateTime timeOfReceipt, object groupId, CommDirection direction, bool wasReceived, TimeSpan? timePendingResponse=null, TimeSpan? duration=null)
        {
            TimeOfReceipt = timeOfReceipt;
            GroupId = groupId;
            Direction = direction;
            WasReceived = wasReceived;

            TimeSpentPending = (timePendingResponse.HasValue) ? timePendingResponse.Value : new TimeSpan(0);
            Duration = (duration.HasValue) ? duration.Value : new TimeSpan(0);
        }
    }

    public class Account : IEnumerable<ICommunication>
    {
        private List<ICommunication> communications = new List<ICommunication>();
        private List<TimeTracker> timeTrackers = new List<TimeTracker>();

        public string Name
        {
            get; set;
        }

        public int[] GroupIds
        {
            get; set;
        }

        public Account(string name, int[] groupIds)
        {
            Name = name;
            GroupIds = groupIds;
        }

        public void AddCommunication(ICommunication comm)
        {
            bool tracked = false;
            foreach (var tracker in timeTrackers)
            {
                try
                {
                    bool tracksComm = tracker.TrackIfSupported(comm);
                    if (tracked)
                    {
                        communications.Add(comm);
                        tracked = true;
                    }
                }
                catch (Exception e)
                {
                    ReportRunner.log.TraceEvent(TraceEventType.Error, 1, $"Encountered an error trying to add '{comm}' to tracker: {e.Message}");
                }
            }

            if (!tracked)
            {
                throw new ArgumentException($"Communication is unsupported by any trackers: {comm}");
            }
        }

        public void RegisterTrackers(IEnumerable<TimeTracker> trackers)
        {
            timeTrackers.AddRange(trackers);
        }

        public IEnumerator<ICommunication> GetEnumerator()
        {
            return communications.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return communications.GetEnumerator();
        }
    }

    public class Accounts : IEnumerable<Account>, IDictionary<object, TimeTracker>
    {
        private int sentinel;
        private Dictionary<object, TimeTracker> trackers;
        private Dictionary<object, Account> accounts = new Dictionary<object, Account>();

        public ICollection<object> Keys
        {
            get
            {
                return ((IDictionary<object, TimeTracker>)trackers).Keys;
            }
        }

        public ICollection<TimeTracker> Values
        {
            get
            {
                return ((IDictionary<object, TimeTracker>)trackers).Values;
            }
        }

        public int Count
        {
            get
            {
                return ((IDictionary<object, TimeTracker>)trackers).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IDictionary<object, TimeTracker>)trackers).IsReadOnly;
            }
        }

        public TimeTracker this[object key]
        {
            get
            {
                return ((IDictionary<object, TimeTracker>)trackers)[key];
            }

            set
            {
                ((IDictionary<object, TimeTracker>)trackers)[key] = value;
            }
        }

        public Accounts(int sentinel, Dictionary<object, TimeTracker> trackers)
        {
            this.sentinel = sentinel;
            this.trackers = trackers;

            var cfg = ConfigurationManager.GetSection("accounts") as AccountsConfiguration;
            foreach (AccountsElement elem in cfg.Accounts)
            {
                var account = new Account(elem.AccountName, elem.AccountCodes);
                account.RegisterTrackers(trackers.Values);

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

        public IEnumerator<Account> GetEnumerator()
        {
            return accounts.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return accounts.Values.GetEnumerator();
        }

        public bool ContainsKey(object key)
        {
            return ((IDictionary<object, TimeTracker>)trackers).ContainsKey(key);
        }

        public void Add(object key, TimeTracker value)
        {
            ((IDictionary<object, TimeTracker>)trackers).Add(key, value);
        }

        public bool Remove(object key)
        {
            return ((IDictionary<object, TimeTracker>)trackers).Remove(key);
        }

        public bool TryGetValue(object key, out TimeTracker value)
        {
            return ((IDictionary<object, TimeTracker>)trackers).TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<object, TimeTracker> item)
        {
            ((IDictionary<object, TimeTracker>)trackers).Add(item);
        }

        public void Clear()
        {
            ((IDictionary<object, TimeTracker>)trackers).Clear();
        }

        public bool Contains(KeyValuePair<object, TimeTracker> item)
        {
            return ((IDictionary<object, TimeTracker>)trackers).Contains(item);
        }

        public void CopyTo(KeyValuePair<object, TimeTracker>[] array, int arrayIndex)
        {
            ((IDictionary<object, TimeTracker>)trackers).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<object, TimeTracker> item)
        {
            return ((IDictionary<object, TimeTracker>)trackers).Remove(item);
        }

        IEnumerator<KeyValuePair<object, TimeTracker>> IEnumerable<KeyValuePair<object, TimeTracker>>.GetEnumerator()
        {
            return ((IDictionary<object, TimeTracker>)trackers).GetEnumerator();
        }
    }
}