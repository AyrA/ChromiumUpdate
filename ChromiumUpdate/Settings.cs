using System;
using System.Diagnostics;
using System.IO;

namespace ChromiumUpdate
{
    public static class Settings
    {
        private class Config
        {
            public int LastVersion;
            public DateTime LastSearch;
            public bool Portable;
        }

        private static Config C;
        private static string _SettingsFile;

        private static string SettingsFile
        {
            get
            {
                if (string.IsNullOrEmpty(_SettingsFile))
                {
                    using (var P = Process.GetCurrentProcess())
                    {
                        _SettingsFile = Path.Combine(Path.GetDirectoryName(P.MainModule.FileName), "config.json");
                    }
                }
                return _SettingsFile;
            }
        }

        public static int LastVersion
        {
            get
            {
                return GET().LastVersion;
            }
            set
            {
                C = GET();
                C.LastVersion = value;
                SET(C);
            }
        }

        public static bool Portable
        {
            get
            {
                return GET().Portable;
            }
            set
            {
                C = GET();
                C.Portable = value;
                SET(C);
            }
        }

        public static DateTime LastSearch
        {
            get
            {
                return GET().LastSearch.ToUniversalTime();
            }
            set
            {
                C = GET();
                C.LastSearch = value.ToUniversalTime();
                SET(C);
            }
        }

        private static Config GET()
        {
            if (C != null)
            {
                return C;
            }
            return C = (File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).FromJson<Config>() : new Config());
        }

        private static Config SET(Config C)
        {
            File.WriteAllText(SettingsFile, C.ToJson());
            return C;
        }
    }
}
