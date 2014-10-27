using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;
using ICSharpCode.SharpZipLib.Zip;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    internal class UpdateHonorbuddyState : State
    {
        #region Fields

        private const string HbUpdateUrl = "http://updates.buddyauth.com/GetNewest?filter=Honorbuddy";
        private const string HbBetaUpdateUrl = "http://updates.buddyauth.com/GetNewest?filter=HonorbuddyBeta";
        private const string HbVersionUrl = "http://updates.buddyauth.com/GetVersion?filter=Honorbuddy";
        private const string HbBetaVersionUrl = "http://updates.buddyauth.com/GetVersion?filter=HonorbuddyBeta";
        private readonly HonorbuddyManager _hbManager;
        private DateTime _lastUpdateCheck;

        string[] _dllNames = new[]
                             {
                               "fasmdll_managed.dll",
                               "RemoteASMNative.dll",
                               "System.Data.SQLite.dll",
                               "Tripper.RecastManaged.dll",
                               "Tripper.Tools.dll",
                             };

        #endregion

        #region Constructors

        public UpdateHonorbuddyState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion

        #region State Members

        public override int Priority
        {
            get { return 1000; }
        }

        public override bool NeedToRun
        {
            get
            {
                return _hbManager.IsRunning && (_hbManager.BotProcess == null || _hbManager.BotProcess.HasExitedSafe()) 
                    && HbRelogManager.Settings.AutoUpdateHB && DateTime.Now - _lastUpdateCheck >= TimeSpan.FromMinutes(30);
            }
        }

        public override void Run()
        {
            if (!File.Exists(_hbManager.Settings.HonorbuddyPath))
            {
                _hbManager.Profile.Pause();
                _hbManager.Profile.Log(string.Format("path to honorbuddy.exe does not exist: {0}", _hbManager.Settings.HonorbuddyPath));
            }
            Log.Write("Checking for new  Honorbuddy update");
            // get local honorbuddy file version.
            FileVersionInfo localFileVersionInfo = FileVersionInfo.GetVersionInfo(_hbManager.Settings.HonorbuddyPath);
            // download the latest Honorbuddy version string from server
            var client = new WebClient {Proxy = null};
            string latestHbVersion = client.DownloadString(_hbManager.Profile.Settings.HonorbuddySettings.UseHBBeta ? HbBetaVersionUrl : HbVersionUrl);
            // check if local version is different from remote honorbuddy version.
            if (localFileVersionInfo.FileVersion != latestHbVersion)
            {
                Log.Write("New version of Honorbuddy is available.");
                var originalFileName = Path.GetFileName(_hbManager.Settings.HonorbuddyPath);
                // close all instances of Honorbuddy
                Log.Write("Closing all instances of Honorbuddy");
                var psi = new ProcessStartInfo("taskKill", "/IM " + originalFileName) {WindowStyle = ProcessWindowStyle.Hidden};

                Process.Start(psi);
                // download the new honorbuddy zip
                Log.Write("Downloading new version of Honorbuddy");
                _hbManager.Profile.Status = "Downloading new version of HB";
                string tempFileName = Path.GetTempFileName();

                client.DownloadFile(_hbManager.Profile.Settings.HonorbuddySettings.UseHBBeta ? HbBetaUpdateUrl : HbUpdateUrl, tempFileName);
                //Log.Write("Deleting old .exe and .dll files");

                //var assembliesToDelete = new List<string>(_dllNames);
                //assembliesToDelete.Add(originalFileName);
                //foreach (var fileName in assembliesToDelete)
                //{
                //    var fullPath = Path.Combine(Settings.HonorbuddyPath, fileName);
                //    if (File.Exists(fullPath))
                //    {
                //        try
                //        {
                //            File.Delete(fullPath);
                //        }
                //        catch { }
                //    }
                //}

                // extract the downloaded zip
                var hbFolder = Path.GetDirectoryName(_hbManager.Settings.HonorbuddyPath);
                Log.Write("Extracting Honorbuddy to {0}", hbFolder);
                _hbManager.Profile.Status = "Extracting Honorbuddy";
                var zip = new FastZip();
                zip.ExtractZip(tempFileName, hbFolder, FastZip.Overwrite.Always, s => true, ".*", ".*", false);

                // delete the downloaded zip
                Log.Write("Deleting temporary file");
                File.Delete(tempFileName);

                // rename the Honorbuddy.exe if original .exe was different
                if (originalFileName != "Honorbuddy.exe")
                {
                    File.Delete(_hbManager.Settings.HonorbuddyPath);
                    Log.Write("Renaming Honorbuddy.exe to {0}", originalFileName);
                    File.Move(Path.Combine(hbFolder, "Honorbuddy.exe"), _hbManager.Settings.HonorbuddyPath);
                }
            }
            else
                Log.Write("Honorbuddy is up-to-date");
            _lastUpdateCheck = DateTime.Now;
        }

        #endregion
    }
}