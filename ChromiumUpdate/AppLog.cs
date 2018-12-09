using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ChromiumUpdate
{
    public enum LogEngine
    {
        Null,
        EventLog,
        EventLogDefault,
        File,
        Stream
    }

    public static class AppLog
    {
        private const string LOG_SOURCE = "Chromium Updater";
        private const string LOG_NAME = "Application";
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
        private const string CRLF = "\r\n";

        private static string _LogPath;

        public static string LogFile
        {
            get
            {
                if (string.IsNullOrEmpty(_LogPath))
                {
                    using (var P = Process.GetCurrentProcess())
                    {
                        _LogPath = Path.GetDirectoryName(P.MainModule.FileName);
                    }
                }
                return Path.Combine(_LogPath, BackendData == null ? "Log.txt" : BackendData.ToString());
            }
        }
        public static LogEngine Backend
        { get; set; }

        public static object BackendData
        { get; set; }

        static AppLog()
        {
            Backend = LogEngine.EventLogDefault;
        }

        public static void WriteInfo(string Message)
        {
            Write(Message, EventLogEntryType.Information);
        }
        public static void WriteWarn(string Message)
        {
            Write(Message, EventLogEntryType.Error);
        }
        public static void WriteError(string Message)
        {
            Write(Message, EventLogEntryType.Error);
        }

        public static void WriteException(string Message, Exception ex)
        {
            var ErrorList = new List<string>();
            while (ex != null)
            {
                ErrorList.Add(FormatException(ex));
                if (ex is AggregateException)
                {
                    ErrorList.AddRange(((AggregateException)ex).InnerExceptions.Select(m => FormatException(m)));
                }
                ex = ex.InnerException;
            }
            var ErrorText = string.Join(CRLF, ErrorList);

            switch (Backend)
            {
                case LogEngine.File:
                    File.AppendAllText(LogFile, FormatMessage(ErrorText, EventLogEntryType.Error) + CRLF);
                    break;
                case LogEngine.EventLog:
                case LogEngine.EventLogDefault:
                    using (var EL = GetLog())
                    {
                        EL.WriteEntry(Message, EventLogEntryType.Error, 101, 1, Encoding.UTF8.GetBytes(ErrorText));
                    }
                    break;
                case LogEngine.Stream:
                    var S = ((Stream)BackendData);
                    using (var SW = new StreamWriter(S, Encoding.UTF8, 1024, true))
                    {
                        SW.WriteLine(FormatMessage(ErrorText, EventLogEntryType.Error));
                        SW.Flush();
                    }
                    break;
                case LogEngine.Null:
                    break;
                default:
                    throw new NotImplementedException($"Log type {Backend} not implemented");
            }
        }

        private static string FormatException(Exception ex)
        {
            return string.Format(@"{0}: {1}

Trace:
{2}

Data:
{3}
=====",
                ex.GetType().FullName,
                ex.Message,
                ex.StackTrace,
                string.Join("\r\n", GetData(ex.Data)));
        }

        private static IEnumerable<string> GetData(IDictionary Data)
        {
            foreach (var E in Data.Keys)
            {
                yield return string.Format("{0}={1}", E, Data[E]);
            }
        }

        private static void Write(string Message, EventLogEntryType EntryType)
        {
            switch (Backend)
            {
                case LogEngine.File:
                    File.AppendAllText(LogFile, FormatMessage(Message, EntryType) + CRLF);
                    break;
                case LogEngine.EventLog:
                case LogEngine.EventLogDefault:
                    RegisterSource();
                    using (var EL = GetLog())
                    {
                        EL.WriteEntry(Message, EntryType);
                    }
                    break;
                case LogEngine.Stream:
                    var S = ((Stream)BackendData);
                    var B = Encoding.UTF8.GetBytes(FormatMessage(Message, EntryType) + CRLF);
                    S.Write(B, 0, B.Length);
                    S.Flush();
                    break;
                case LogEngine.Null:
                    break;
                default:
                    throw new NotImplementedException($"Log type {Backend} not implemented");
            }
        }

        private static string FormatMessage(string Message, EventLogEntryType EntryType)
        {
            return string.Format("[{0}]\t{1}\t{2}", DateTime.UtcNow.ToString(DATE_FORMAT), EntryType, Message);
        }

        private static EventLog GetLog()
        {
            EventLog EL = new EventLog(LOG_NAME);
            EL.Source = RegisterSource();
            return EL;
        }

        private static string RegisterSource()
        {
            if (Backend == LogEngine.EventLog)
            {
                EventLog.CreateEventSource(LOG_SOURCE, LOG_NAME);
                return LOG_SOURCE;
            }
            return LOG_NAME;
        }
    }
}
