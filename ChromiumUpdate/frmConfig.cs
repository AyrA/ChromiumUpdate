using AyrA.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Forms;

namespace ChromiumUpdate
{
    public partial class frmConfig : Form
    {
        private string ExePath;
        private string UpdateDir;
        private string LinkName;

        public frmConfig()
        {
            InitializeComponent();
            using (var P = Process.GetCurrentProcess())
            {
                ExePath = P.MainModule.FileName;
            }
            UpdateDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\ChromiumUpdate");
            LinkName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Chromium Update.lnk");
            CheckBtn();
        }

        private void CheckBtn()
        {
            btnUninstall.Visible = File.Exists(LinkName) || Chromium.IsInstalled();
        }

        private void SetEnabled(bool State)
        {
            foreach (var Control in Controls)
            {
                ((Control)Control).Enabled = State;
            }
            CheckBtn();
        }

        private void KillUpdater(bool DeleteDirectory)
        {
            if (Directory.Exists(UpdateDir))
            {
                var Processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
                foreach (var P in Processes)
                {
                    if (P.MainModule.FileName == Path.Combine(UpdateDir, Path.GetFileName(ExePath)))
                    {
                        try
                        {
                            P.Kill();
                        }
                        catch (Exception ex)
                        {
                            AppLog.WriteException("Unable to kill updater", ex);
                        }
                    }
                    P.Dispose();
                }
                if (DeleteDirectory)
                {
                    try
                    {
                        Directory.Delete(UpdateDir, true);
                    }
                    catch (Exception ex)
                    {
                        AppLog.WriteException($"Unable to delete updater directory: {UpdateDir}", ex);
                    }
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            if (!cbChromium.Checked && !cbUpdate.Checked)
            {
                MessageBox.Show("Please select at least one component", Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (cbUpdate.Checked)
            {
                var ExeName = Path.GetFileName(ExePath);
                var Link = (IShellLink)new ShellLink();
                if (!Directory.Exists(UpdateDir))
                {
                    Directory.CreateDirectory(UpdateDir);
                }
                try
                {
                    File.Copy(ExePath, Path.Combine(UpdateDir, ExeName), true);
                }
                catch (Exception ex)
                {
                    if (!File.Exists(Path.Combine(UpdateDir, ExeName)))
                    {
                        AppLog.WriteException("Unable to install updater", ex);
                    }
                }
                if (File.Exists(Path.Combine(UpdateDir, ExeName)))
                {
                    Link.SetPath(Path.Combine(UpdateDir, ExeName));
                    Link.SetWorkingDirectory(UpdateDir);
                    Link.SetDescription("Keeps Chromium up to date");
                    Link.SetArguments("/update");
                    Link.SetIconLocation(Path.Combine(UpdateDir, ExeName), 0);
                    if (File.Exists(LinkName))
                    {
                        File.Delete(LinkName);
                    }
                    ((IPersistFile)Link).Save(LinkName, false);

                    if (!cbChromium.Checked)
                    {
                        MessageBox.Show("Installation complete", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    var ErrMsg = "Installation of updater failed.";
                    if (cbChromium.Checked)
                    {
                        ErrMsg += ". Will continue with Chromium installation now.";
                    }
                    else
                    {
                        MessageBox.Show(ErrMsg, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            if (cbChromium.Checked)
            {
                SetEnabled(false);
                var T = new Thread(delegate ()
                {
                    var Result = Chromium.Install(true);
                    Invoke((MethodInvoker)delegate
                    {
                        var Msg = "An unknown error occured";
                        switch (Result)
                        {
                            case ChromiumInstallResult.BinaryDownloadFailed:
                            case ChromiumInstallResult.MetaDownloadFailed:
                            case ChromiumInstallResult.RemoteListError:
                            case ChromiumInstallResult.VersionDownloadFailed:
                                Msg = "Network error. We are unable to download the necessary files right now.";
                                break;
                            case ChromiumInstallResult.Success:
                            case ChromiumInstallResult.NoNewVersion:
                                Msg = null;
                                break;
                            case ChromiumInstallResult.InstallFailed:
                                Msg = "Installer error. We are unable to install chromium right now.";
                                break;
                        }
                        MessageBox.Show(Msg == null ? "Installation complete" : Msg, Text, MessageBoxButtons.OK, Msg == null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        SetEnabled(true);
                    });
                });
                T.IsBackground = true;
                T.Name = "Chromium Installer";
                T.Start();
            }
            else
            {
                SetEnabled(true);
            }
        }

        private void btnUninstall_Click(object sender, EventArgs e)
        {
            if (!cbChromium.Checked && !cbUpdate.Checked)
            {
                MessageBox.Show("Please select at least one component", Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (cbUpdate.Checked)
            {
                if (File.Exists(LinkName))
                {
                    File.Delete(LinkName);
                }
                KillUpdater(true);
            }
            if (cbChromium.Checked && Chromium.IsInstalled())
            {
                SetEnabled(false);
                using (var P = Process.Start(Chromium.GetUninstallerPath(), Chromium.GetUninstallerArgs()))
                {
                    P.WaitForExit();
                }
            }
            MessageBox.Show("Uninstallation complete", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetEnabled(true);
        }
    }
}
