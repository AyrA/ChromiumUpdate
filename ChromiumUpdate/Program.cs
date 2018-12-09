using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ChromiumUpdate
{
    class Program
    {
        private const string META_URL = @"https://www.googleapis.com/storage/v1/b/chromium-browser-snapshots/o/Win%2FLAST_CHANGE";
        private const string COMMIT_URL = @"https://www.googleapis.com/storage/v1/b/chromium-browser-snapshots/o?delimiter=/&prefix=Win/{0}/&fields=*";
        private const string PORTABLE = @"chrome-win.zip";
        private const string INSTALLER = @"mini_installer.exe";
        private const string TEMP_INSTALLER = @"%TEMP%\chromium_update.exe";

        private struct ERR
        {
            public const int OK = 0;
            public const int REMOTE = 1;
            public const int META = 2;
            public const int DOWNLOAD_ID = 3;
            public const int UNKNOWN = 255;
        }

        static int Main(string[] args)
        {
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
                var CommitFileInfo = GetMetadata(new Uri(META_URL));
                if (CommitFileInfo.mediaLink == null)
                {
                    return ERR.META;
                }
                var CommitId = 0;
                try
                {
                    CommitId = GetCommit(CommitFileInfo.mediaLink);
                }
                catch (Exception ex)
                {
                    AppLog.WriteException("Unable to obtain Commit Id", ex);
                    return ERR.REMOTE;
                }

                if (Forced || Settings.LastVersion < CommitId)
                {
                    UpdateChromium(CommitId);
                }
                else
                {
                    AppLog.WriteInfo($"No new update. Local={Settings.LastVersion}; Remote={CommitId}");
                    Settings.LastSearch = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteException("Uncaught exception", ex);
                return ERR.UNKNOWN;
            }
            Console.ReadKey(true);
            return ERR.OK;
        }

        private static byte[] ReadFileMem(Uri UrlToFile)
        {
            try
            {
                using (var S = HTTP.Get(UrlToFile))
                {
                    return S.ReadAll();
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteException($"Unable to obtain content from {UrlToFile}", ex);
            }
            return null;
        }

        private static bool ReadFileDisk(Uri UrlToFile, string NewFileName)
        {
            bool CreatedFile = false;
            try
            {
                using (var FS = File.Create(NewFileName))
                {
                    CreatedFile = true;
                    using (var S = HTTP.Get(UrlToFile))
                    {
                        S.CopyTo(FS);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteException($"Unable to obtain metadata from {UrlToFile}", ex);
                if (CreatedFile)
                {
                    try
                    {
                        File.Delete(NewFileName);
                    }
                    catch (Exception ex2)
                    {
                        AppLog.WriteException($"Unable to delete failed file {NewFileName}", ex2);
                    }
                }
            }
            return false;
        }

        private static ObjectMetadata GetMetadata(Uri UrlToFile)
        {
            try
            {
                using (var Meta = HTTP.Get(META_URL))
                {
                    return Meta.ToString(Encoding.UTF8).FromJson<ObjectMetadata>();
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteException($"Unable to obtain metadata from {UrlToFile}", ex);
            }
            return default(ObjectMetadata);
        }

        private static int GetCommit(Uri UrlToFile)
        {
            using (var Content = HTTP.Get(UrlToFile))
            {
                var Body = Content.ToString(Encoding.UTF8).Trim().TrimRight(20);
                var CommitId = 0;
                if (int.TryParse(Body, out CommitId) && CommitId >= 0)
                {
                    return CommitId;
                }
                else
                {
                    throw new FormatException($"Commit ID is invalid. Should be positive integer but is {Body}");
                }
            }
        }

        private static bool UpdateChromium(int CommitId)
        {
            AppLog.WriteInfo($"Need update. Local={Settings.LastVersion}; Remote={CommitId}");
            using (var Listing = HTTP.Get(string.Format(COMMIT_URL, CommitId)))
            {
                var List = Listing.ToString(Encoding.UTF8).FromJson<ObjectList>();
                if (Settings.Portable)
                {
                    var ZipFile = List.items.FirstOrDefault(m => m.name.EndsWith($"/{PORTABLE}"));
                    if (ZipFile.mediaLink != null)
                    {

                    }
                    else
                    {
                        AppLog.WriteError("List misses portable package. Try again later");
                    }
                }
                else
                {
                    var Installer = List.items.FirstOrDefault(m => m.name.EndsWith($"/{INSTALLER}"));
                    if (Installer.mediaLink != null)
                    {
                        var InstallerFileName = Environment.ExpandEnvironmentVariables(TEMP_INSTALLER);
                        if (ReadFileDisk(Installer.mediaLink, InstallerFileName))
                        {
                            var NewVersion = new Version(FileVersionInfo.GetVersionInfo(InstallerFileName).FileVersion);
                            var CurrentVersion = GetInstalledVersion();
                            if (NewVersion > CurrentVersion)
                            {
                                AppLog.WriteInfo($"Installation complete. Exit code: {RunAndKill(InstallerFileName)}");
                                AppLog.WriteInfo("Update installed");
                            }
                            else
                            {
                                AppLog.WriteInfo("New Setup reports identical version");
                            }
                            Settings.LastVersion = CommitId;
                            Settings.LastSearch = DateTime.UtcNow;
                            return true;
                        }
                    }
                    else
                    {
                        AppLog.WriteError("List misses installer package. Try again later");
                    }
                }
            }
            return false;
        }

        private static int RunAndKill(string Application)
        {
            int Code = 0;
#if !DEBUG
            using (var P = Process.Start(Application))
            {
                P.WaitForExit();
                Code = P.ExitCode;
            }
#endif
            try
            {
                File.Delete(Application);
            }
            catch
            {
                AppLog.WriteWarn($"Unable to delete {Application}");
            }
            return Code;
        }

        private static Version GetInstalledVersion()
        {
            var Current = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Chromium", "pv", "1.0.0.0");
            Version Ret = null;
            if (Current == null || !Version.TryParse(Current, out Ret))
            {
                return new Version("1.0.0.0");
            }
            return Ret;
        }
    }
}