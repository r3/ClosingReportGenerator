using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace ClosingReport
{
    [Serializable]
    public class ParseException : Exception
    {
        public ParseException()
        {
        }

        public ParseException(string message)
            : base(message)
        {
        }

        public ParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }


    public class MailClient
    {
        public SmtpClient Client
        {
            get; set;
        }

        private int? _port = null;
        public int Port
        {
            get
            {
                if (_port.HasValue)
                {
                    return _port.Value;
                }

                string configuredPort = ConfigurationManager.AppSettings["SMTPPort"];
                int parsedPort;
                if (int.TryParse(configuredPort, out parsedPort))
                {
                    _port = parsedPort;
                    return parsedPort;
                }

                throw new ParseException($"Failed to parse 'SMTPPort' as integer, got: {configuredPort}");
            }
            set
            {
                _port = value;
            }
        }

        private NetworkCredential _creds = null;
        public NetworkCredential Credentials
        {
            get
            {
                if (_creds == null)
                {
                    try
                    {
                        string username = ConfigurationManager.AppSettings["SMTPUser"];
                        string password = ConfigurationManager.AppSettings["SMTPPassword"];
                        _creds = new NetworkCredential(username, password);
                    }
                    catch (ConfigurationException)
                    {
                    }
                }
                return _creds;
            }
            set
            {
                _creds = value;
            }
        }

        private string _host = null;
        public string Host
        {
            get
            {
                if (_host == null)
                {
                    try
                    {
                        _host = ConfigurationManager.AppSettings["SMTPAddress"];
                    }
                    catch (ConfigurationException)
                    {
                    }
                }
                return _host;
            }
            set
            {
                _host = value;
            }
        }

        public MailClient()
        {
            Client = new SmtpClient();
            Client.Port = Port;
            Client.Host = Host;
            Client.EnableSsl = true;
            Client.DeliveryMethod = SmtpDeliveryMethod.Network;
            Client.Timeout = 1000000;
            if (Credentials != null)
            {
                //Client.UseDefaultCredentials = false;
                Client.Credentials = Credentials;
            }
        }
    }

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
            catch (Exception e )
            {
                throw new ArgumentException($"Unable to read value with key, '{name}' from the configuration file with error: {e.Message}");
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
                ClosingReport.log.TraceEvent(TraceEventType.Warning, 1, $"Unable to compute average, no TimeSpan objects in enumerable");
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
        private SortedDictionary<TimeSpan, int> counts;
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

            counts = new SortedDictionary<TimeSpan, int>();
            TimeSpan timeIndex = TimeManagement.OpeningTime;
            TimeSpan increment = TimeSpan.FromMinutes(TimeManagement.Increment);

            while (timeIndex <= TimeManagement.ClosingTime)
            {
                counts[timeIndex] = 0;
                timeIndex += increment;
            }
        }

        public bool TrackIfSupported(ICommunication comm)
        {
            if (IsTrackable(comm))
            {
                TimeSpan rounded = TimeManagement.NearestIncrement(comm.TimeOfReceipt);
                ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Tracker {Name} supports communication, '{comm};' adding as {rounded}");
                counts[rounded]++;
                return true;
            }

            ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Tracker {Name} does not support communication: {comm}");
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
        public static Func<string[], ICommunication> ResourceAppropriateFactory(ResourceElement resource)
        {
            if (resource.ResourceDirection == CommDirection.Inbound)
            {
                if (resource.ResourceReceived)
                {
                    return CommunicationFactories.fromInboundRecord;
                }
                else
                {
                    return CommunicationFactories.fromAbandonedRecord;
                }
            }
            else
            {
                return CommunicationFactories.fromOutboundRecord;
            }

        }

        private static ICommunication fromInboundRecord(string[] row)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(row[0]);
                string telephoneNumber = row[1];
                TimeSpan callDuration = TimeManagement.StampToSpan(row[2]);

                int agentId = ClosingReport.sentinel;
                int.TryParse(row[3], out agentId);

                int accountCode = ClosingReport.sentinel;
                int.TryParse(row[4], out accountCode);

                TimeSpan ringDuration = TimeManagement.StampToSpan(row[5]);

                Communication comm = new Communication(firstRingTime, accountCode, CommDirection.Inbound, true, ringDuration, callDuration);
                ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse communication from CVS row: {row}. Got error: {e.Message}");
            }
        }

        private static ICommunication fromOutboundRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);
                string telephoneNumber = record[1];
                TimeSpan callDuration = TimeManagement.StampToSpan(record[2]);

                int agentId = ClosingReport.sentinel;
                int.TryParse(record[3], out agentId);

                int accountCode = ClosingReport.sentinel;
                int.TryParse(record[4], out accountCode);

                ICommunication comm = new Communication(firstRingTime, accountCode, CommDirection.Outbound, true, null, callDuration);
                ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }

        private static ICommunication fromAbandonedRecord(string[] record)
        {
            try
            {
                DateTime firstRingTime = DateTime.Parse(record[0]);

                int accountCode = ClosingReport.sentinel;
                int.TryParse(record[1], out accountCode);

                TimeSpan callDuration = TimeManagement.StampToSpan(record[2]);
                ICommunication comm = new Communication(firstRingTime, accountCode, CommDirection.Inbound, false, callDuration);
                ClosingReport.log.TraceEvent(TraceEventType.Information, 0, $"Parsed communication: {comm}");
                return comm;
            }
            catch (Exception e)
            {
                throw new ParseException($"Unable to parse call from CVS row: {record}. Got error: {e.Message}");
            }
        }
    }

    class CommunicationProcessor
    {
        private List<Action<ICommunication>> callbacks;

        public CommunicationProcessor()
        {
            callbacks = new List<Action<ICommunication>>();
        }

        private IEnumerable<string[]> IterRecords(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                throw new ArgumentException($"Could not open file at, '{csvPath}'");
            }

            using (var fs = File.OpenRead(csvPath))
            using (var reader = new StreamReader(fs))
            {
                reader.ReadLine();  // Skip header
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] splitted = line.Split(',');

                    yield return splitted.Select(x => x.Trim('"')).ToArray();
                }
            }
            yield break;
        }

        public void RegisterCallback(Action<ICommunication> callback)
        {
            callbacks.Add(callback);
        }

        public void ProcessResource(ResourceElement resource)
        {
            Func<string[], ICommunication> factory = CommunicationFactories.ResourceAppropriateFactory(resource);

            foreach (string[] record in IterRecords(resource.ResourcePath))
            {
                ICommunication call = factory(record);
                foreach (var callback in callbacks)
                {
                    callback(call);
                }
            }
        }
    }
}
