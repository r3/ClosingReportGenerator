﻿using System;
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

        Int32 GroupId
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

        public Int32 GroupId
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

        public Communication(DateTime timeOfReceipt, int groupId, CommDirection direction, bool wasReceived, TimeSpan? timePendingResponse=null, TimeSpan? duration=null)
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
        internal List<ICommunication> communications;
        private List<TimeTracker> timeTrackers;

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
            communications = new List<ICommunication>();
            timeTrackers = new List<TimeTracker>();
        }

        public void AddCommunication(ICommunication comm)
        {
            bool wasTracked = false;
            foreach (var tracker in timeTrackers)
            {
                try
                {
                    if (tracker.TrackIfSupported(comm))
                    {
                        communications.Add(comm);
                        wasTracked = true;
                    }
                }
                catch (Exception e)
                {
                    ClosingReport.log.TraceEvent(TraceEventType.Error, 2, $"Encountered an error trying to add '{comm}' to tracker: {e.Message}");
                }
            }

            if (!wasTracked)
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

    public class Accounts : IEnumerable<Account>
    {
        private int sentinel;
        private Dictionary<object, Account> accounts;
        private List<int> filteredIds;

        public Dictionary<object, TimeTracker> Trackers
        {
            get; private set;
        }

        public IEnumerable<Account> UniqueAccounts
        {
            get
            {
                return new HashSet<Account>(accounts.Values);
            }
        }

        public Accounts(int sentinel, Dictionary<object, TimeTracker> trackers, List<int> filterIds)
        {
            this.sentinel = sentinel;
            Trackers = trackers;
            accounts = new Dictionary<object, Account>();
            filteredIds = filterIds;

            var cfg = ClosingReport.config.GetSection("accounts") as AccountsConfiguration;
            foreach (AccountsElement elem in cfg.Accounts)
            {
                var account = new Account(elem.AccountName, elem.AccountCodes);
                account.RegisterTrackers(Trackers.Values);

                foreach (var code in elem.AccountCodes)
                {
                    if (!accounts.Keys.Contains(code))
                    {
                        accounts[code] = account;
                        ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Adding account, '{account.Name}' with code, '{code}'");
                    }
                    else
                    {
                        string err = $"Could not add {account.Name} to accounts with code, {code}. Account code already used by {accounts[code].Name}";
                        ClosingReport.log.TraceEvent(TraceEventType.Error, 1, err);
                        throw new ArgumentException(err);
                    }
                }
            }
        }

        public void AddCommunication(ICommunication comm)
        {
            if (filteredIds.Contains(comm.GroupId))
            {
                ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Ignoring communication with filtered ID: {comm}");
                return;
            }

            if (!TimeManagement.IsOpenAt(comm.TimeOfReceipt))
            {
                ClosingReport.log.TraceEvent(TraceEventType.Warning, 0, $"Communication falls outside hours of operation: {comm}");
                return;
            }

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
                ClosingReport.log.TraceEvent(TraceEventType.Error, 1, $"Unable to add call, {comm}, got error: {e.Message}");
            }
        }

        public Account GetAccount(int accountCode)
        {
            return accounts[accountCode];
        }

        public IEnumerator<Account> GetEnumerator()
        {
            return new HashSet<Account>(accounts.Values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new HashSet<Account>(accounts.Values).GetEnumerator();
        }
    }
}