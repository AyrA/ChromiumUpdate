using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ChromiumUpdate
{
    class Chromium
    {
        private const string META_URL = @"https://www.googleapis.com/storage/v1/b/chromium-browser-snapshots/o/Win%2FLAST_CHANGE";
        private const string COMMIT_URL = @"https://www.googleapis.com/storage/v1/b/chromium-browser-snapshots/o?delimiter=/&prefix=Win/{0}/&fields=*";
        private const string PORTABLE = @"chrome-win.zip";
        private const string INSTALLER = @"mini_installer.exe";
        private const string TEMP_INSTALLER = @"%TEMP%\chromium_update.exe";

        public static bool IsInstalled()
        {
            var Success = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Chromium", "InstallerSuccessLaunchCmdLine", string.Empty);
            return !string.IsNullOrEmpty(Success);
        }

        public static string GetUninstallerPath()
        {
            var Data = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Chromium", "UninstallString", string.Empty);
            return string.IsNullOrEmpty(Data) ? null : Data;
        }

        public static string GetUninstallerArgs()
        {
            var Data = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Chromium", "UninstallArguments", string.Empty);
            return string.IsNullOrEmpty(Data) ? null : Data;
        }

        public static Version GetInstalledVersion()
        {
            var Current = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Chromium", "pv", "1.0.0.0");
            Version Ret = null;
            if (Current == null || !Version.TryParse(Current, out Ret))
            {
                return new Version("1.0.0.0");
            }
            return Ret;
        }

        public static ChromiumInstallResult Install(bool Force = false)
        {
            var CommitFileInfo = GetMetadata(new Uri(META_URL));
            if (CommitFileInfo.mediaLink == null)
            {
                return ChromiumInstallResult.MetaDownloadFailed;
            }
            var CommitId = 0;
            try
            {
                CommitId = GetCommit(CommitFileInfo.mediaLink);
            }
            catch (Exception ex)
            {
                AppLog.WriteException("Unable to obtain Commit Id", ex);
                return ChromiumInstallResult.VersionDownloadFailed;
            }

            if (Force || Settings.LastVersion < CommitId)
            {
                ChromiumInstallResult InstallResult;
                try
                {
                    InstallResult = UpdateChromium(CommitId, Force);
                }
                catch (Exception ex)
                {
                    AppLog.WriteException("Unable to install Chromium", ex);
                    return ChromiumInstallResult.InstallFailed;
                }
                //TODO: Eval InstallResult
            }
            else
            {
                AppLog.WriteInfo($"No new update. Local={Settings.LastVersion}; Remote={CommitId}");
                Settings.LastSearch = DateTime.UtcNow;
                return ChromiumInstallResult.NoNewVersion;
            }
            return ChromiumInstallResult.Success;
        }

        #region Internals
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

        private static ChromiumInstallResult UpdateChromium(int CommitId, bool Force = false)
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
                        return ChromiumInstallResult.NotImplemented;
                    }
                    else
                    {
                        AppLog.WriteError("List misses portable package. Try again later");
                        return ChromiumInstallResult.RemoteListError;
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
                            if (Force || NewVersion > CurrentVersion)
                            {
                                var ExitCode = RunAndKill(InstallerFileName);
                                AppLog.WriteInfo($"Installation complete. Exit code: {ExitCode}");
                                AppLog.WriteInfo("Update installed");
                            }
                            else
                            {
                                AppLog.WriteInfo("New Setup reports identical version");
                            }
                            Settings.LastVersion = CommitId;
                            Settings.LastSearch = DateTime.UtcNow;
                            return NewVersion > CurrentVersion ? ChromiumInstallResult.Success : ChromiumInstallResult.NoNewVersion;
                        }
                        else
                        {
                            return ChromiumInstallResult.BinaryDownloadFailed;
                        }
                    }
                    else
                    {
                        AppLog.WriteError("List misses installer package. Try again later");
                        return ChromiumInstallResult.RemoteListError;
                    }
                }
            }
            throw new NotImplementedException("Not implemented code path");
        }

        private static int RunAndKill(string Application)
        {
            int Code = 0;
            using (var P = Process.Start(Application))
            {
                P.WaitForExit();
                Code = P.ExitCode;
            }
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
        #endregion
    }

    public enum ChromiumInstallResult
    {
        /// <summary>
        /// Installation success
        /// </summary>
        Success,
        /// <summary>
        /// Unable to download meta file
        /// </summary>
        MetaDownloadFailed,
        /// <summary>
        /// Unable to download version file
        /// </summary>
        VersionDownloadFailed,
        /// <summary>
        /// Unable to download binary
        /// </summary>
        BinaryDownloadFailed,
        /// <summary>
        /// Everything OK, no new version available
        /// </summary>
        NoNewVersion,
        /// <summary>
        /// Unimplemented feature
        /// </summary>
        NotImplemented,
        /// <summary>
        /// Remote list lacks important entries
        /// </summary>
        RemoteListError,
        /// <summary>
        /// Installation failed for an unspecified reason
        /// </summary>
        InstallFailed
    }
}
