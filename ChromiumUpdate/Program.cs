using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ChromiumUpdate
{
    class Program
    {
        private struct ERR
        {
            public const int OK = 0;
            public const int REMOTE = 1;
            public const int META = 2;
            public const int DOWNLOAD_ID = 3;
            public const int USER_CANCEL = 4;
            public const int UNKNOWN = 255;
        }

        static int Main(string[] args)
        {
            if (true || !File.Exists(Settings.SettingsFile) && !args.Any(m => m.ToLower() == "/update"))
            {
                AyrA.IO.Terminal.RemoveConsole();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new frmConfig());
                if (!File.Exists(Settings.SettingsFile))
                {
                    return ERR.USER_CANCEL;
                }
                return ERR.OK;
            }

#if DEBUG
            var Forced = true;
            AppLog.Backend = LogEngine.Stream;
            AppLog.BackendData = Console.OpenStandardError();
            Settings.Portable = false;
#else
            var Forced = args.Any(m => m.ToLower() == "/force");
            //Don't search for an update too often.
            //Using 22.5 allows 30 minutes of slack and accepts the hour shift for DST
            if (!Forced && DateTime.UtcNow.Subtract(Settings.LastSearch).TotalHours < 22.5)
            {
                return ERR.OK;
            }
#endif
            AppLog.WriteInfo("Update started");
            try
            {
                Chromium.Install(Forced);
                AppLog.WriteInfo("Update Complete");
            }
            catch (Exception ex)
            {
                AppLog.WriteException("Uncaught exception", ex);
                return ERR.UNKNOWN;
            }
#if DEBUG
            Console.ReadKey(true);
#endif
            return ERR.OK;
        }
    }
}