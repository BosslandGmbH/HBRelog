/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using HighVoltz.HBRelog.Settings;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32.SafeHandles;

namespace HighVoltz.HBRelog.Honorbuddy
{
    public class HonorbuddyManager : IBotManager
    {
        private const string HbUpdateUrl = "http://updates.buddywing.com/GetNewest?filter=Honorbuddy";
        private const string HbBetaUpdateUrl = "http://updates.buddywing.com/GetNewest?filter=HonorbuddyBeta";
        private const string HbVersionUrl = "http://updates.buddyauth.com/GetVersion?filter=Honorbuddy";
        private const string HbBetaVersionUrl = "http://updates.buddyauth.com/GetVersion?filter=HonorbuddyBeta";

        readonly object _lockObject = new object();
        public bool IsRunning { get; private set; }
        public bool StartupSequenceIsComplete { get; private set; }

        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        CharacterProfile _profile;
        public CharacterProfile Profile
        {
            get { return _profile; }
            private set { _profile = value; Settings = value.Settings.HonorbuddySettings; }
        }
        public HonorbuddySettings Settings { get; private set; }
        public Process BotProcess { get; private set; }

        public HonorbuddyManager(CharacterProfile profile)
        {
            Profile = profile;
        }

        public void SetSettings(HonorbuddySettings settings)
        {
            Settings = settings;
        }

        Timer _hbCloseTimer;
        int _windowCloseAttempt;
        bool _isExiting;
        public void CloseBotProcess()
        {
            if (!_isExiting && BotProcess != null && !BotProcess.HasExited)
            {
                _isExiting = true;
                Profile.Log("Attempting to close Honorbuddy");
                BotProcess.CloseMainWindow();
                _windowCloseAttempt++;
                _hbCloseTimer = new Timer(state =>
                {
                    if (!((Process)state).HasExited)
                    {
                        if (_windowCloseAttempt++ < 15)
                            ((Process)state).CloseMainWindow();
                        else if (_windowCloseAttempt >= 15)
                        {
                            Profile.Log("Killing Honorbuddy");
                            ((Process)state).Kill();
                        }
                    }
                    else
                    {
                        _isExiting = false;
                        Profile.Log("Successfully closed Honorbuddy");
                        BotProcess = null;
                        _windowCloseAttempt = 0;
                        _hbCloseTimer.Dispose();
                    }
                }, BotProcess, 1000, 1000);
            }
        }

        bool _pluginIsUptodate;
        private DateTime _lastUpdateCheck;
        public void Start()
        {
            if (File.Exists(Settings.HonorbuddyPath))
            {
                // check if there is a new version available.
                if (HbRelogManager.Settings.AutoUpdateHB &&  DateTime.Now - _lastUpdateCheck >= TimeSpan.FromMinutes(30))
                {
                    Log.Write("Checking for new  Honorbuddy update");
                    // get local honorbuddy file version.
                    FileVersionInfo localFileVersionInfo = FileVersionInfo.GetVersionInfo(Settings.HonorbuddyPath);
                    // download the latest Honorbuddy version string from server
                    var client = new WebClient { Proxy = null };
                    string latestHbVersion = client.DownloadString( HbRelogManager.Settings.UseHBBeta ? HbBetaVersionUrl: HbVersionUrl);
                    // check if local version is different from remote honorbuddy version.
                    if (localFileVersionInfo.FileVersion != latestHbVersion)
                    {
                        Log.Write("New version of Honorbuddy is available.");
                        var originalFileName = Path.GetFileName(Settings.HonorbuddyPath);
                        // close all instances of Honorbuddy
                        Log.Write("Closing all instances of Honorbuddy");
                        var psi = new ProcessStartInfo("taskKill", "/IM " + originalFileName) {WindowStyle = ProcessWindowStyle.Hidden};

                        Process.Start(psi);
                        // download the new honorbuddy zip
                        Log.Write("Downloading new version of Honorbuddy");
                        Profile.Status = "Downloading new version of HB";
                        string tempFileName = Path.GetTempFileName();

                        client.DownloadFile(HbRelogManager.Settings.UseHBBeta ? HbBetaUpdateUrl : HbUpdateUrl, tempFileName);

                        // extract the downloaded zip
                        var hbFolder = Path.GetDirectoryName(Settings.HonorbuddyPath);
                        Log.Write("Extracting Honorbuddy to {0}", hbFolder);
                        Profile.Status = "Extracting Honorbuddy";
                        var zip = new FastZip();
                        zip.ExtractZip(tempFileName, hbFolder, FastZip.Overwrite.Always, s => true, ".*", ".*", true);

                        // delete the downloaded zip
                        Log.Write("Deleting temporary file");
                        File.Delete(tempFileName);

                        // rename the Honorbuddy.exe if original .exe was different
                        if (originalFileName != "Honorbuddy.exe")
                        {
                            File.Delete(Settings.HonorbuddyPath);
                            Log.Write("Renaming Honorbuddy.exe to {0}", originalFileName);
                            File.Move(Path.Combine(hbFolder, "Honorbuddy.exe"), Settings.HonorbuddyPath);
                        }
                    }
                    else
                        Log.Write("Honorbuddy is up-to-date");
                    _lastUpdateCheck = DateTime.Now;
                }

                // remove internet zone restrictions from Honorbuddy.exe if it exists
                Utility.UnblockFileIfZoneRestricted(Settings.HonorbuddyPath);
                // check if we need to copy over plugin.
                if (!_pluginIsUptodate)
                {
                    using (var reader = new StreamReader(
                        Assembly.GetExecutingAssembly().
                        GetManifestResourceStream("HighVoltz.HBRelog.HBPlugin.HBRelogHelper.cs")))
                    {
                        string pluginString = reader.ReadToEnd();
                        // copy the HBPlugin over to the Honorbuddy plugin folder if it doesn't exist.
                        // or length doesn't match with the version in resource.
                        string pluginFolder = Path.Combine(Path.GetDirectoryName(Settings.HonorbuddyPath),
                                            "Plugins\\HBRelogHelper");
                        if (!Directory.Exists(pluginFolder))
                            Directory.CreateDirectory(pluginFolder);

                        string pluginPath = Path.Combine(pluginFolder, "HBRelogHelper.cs");

                        var fi = new FileInfo(pluginPath);
                        if (!fi.Exists || fi.Length != pluginString.Length)
                        {
                            File.WriteAllText(pluginPath, pluginString);
                        }
                    }
                    _pluginIsUptodate = true;
                }
                IsRunning = true;
                StartHonorbuddy();
            }
            else
                throw new FileNotFoundException(string.Format("path to honorbuddy.exe does not exist: {0}", Settings.HonorbuddyPath));
        }


        bool _waitingToStart;

        void StartHonorbuddy()
        {
            _waitingToStart = true;
        }

        DateTime _hbStartupTimeStamp;
        public void Pulse()
        {
            lock (_lockObject)
            {
                if (IsRunning)
                {
                    var gameProc = Profile.TaskManager.WowManager.GameProcess;
                    if (gameProc == null || gameProc.HasExited)
                    {
                        Stop();
                        return;
                    }
                    if (_isExiting)
                        return;
                    if (_waitingToStart)
                    {  // we need to delay starting honorbuddy for a few seconds if another instance from same path was started a few seconds ago
                        if (HBStartupManager.CanStart(Settings.HonorbuddyPath))
                        {
                            Profile.Log("starting {0}", Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                            Profile.Status = "Starting Honorbuddy";
                            StartupSequenceIsComplete = false;
                            string hbArgs = string.Format("/noupdate /pid={0} /autostart {1}{2}{3}",
                                Profile.TaskManager.WowManager.GameProcess.Id,
                                !string.IsNullOrEmpty(Settings.CustomClass) ? string.Format("/customclass=\"{0}\" ", Settings.CustomClass) : string.Empty,
                                !string.IsNullOrEmpty(Settings.HonorbuddyProfile) ? string.Format("/loadprofile=\"{0}\" ", Settings.HonorbuddyProfile) : string.Empty,
                                !string.IsNullOrEmpty(Settings.BotBase) ? string.Format("/botname=\"{0}\" ", Settings.BotBase) : string.Empty
                                );
                            var hbWorkingDirectory = Path.GetDirectoryName(Settings.HonorbuddyPath);
                            var procStartI = new ProcessStartInfo(Settings.HonorbuddyPath, hbArgs)
                            {
                                WorkingDirectory = hbWorkingDirectory
                            };
                            BotProcess = Process.Start(procStartI);
                            _hbStartupTimeStamp = DateTime.Now;
                            _waitingToStart = false;
                        }
                        else
                            return;
                    }
                    // restart wow hb if it has exited
                    if (BotProcess == null || BotProcess.HasExited)
                    {
                        Profile.Log("Honorbuddy process was terminated. Restarting");
                        Profile.Status = "Honorbuddy has exited.";
                        BotProcess = null;
                        StartupSequenceIsComplete = false;
                        IsRunning = false;
                        return;
                    }
                    // return if hb isn't ready for input.
                    if (!BotProcess.WaitForInputIdle(0))
                        return;

                    // check if it's taking Honorbuddy too long to connect.
                    if (!StartupSequenceIsComplete && DateTime.Now - _hbStartupTimeStamp > TimeSpan.FromMinutes(1))
                    {
                        Profile.Log("Closing Honorbuddy because it took too long to attach");
                        Stop();
                    }
                    if (!HBIsResponding || HBHasCrashed)
                    {
                        if (!HBIsResponding) // we need to kill the process if it's not responding. 
                        {
                            Profile.Log("Honorbuddy is not responding.. So lets restart it");
                            Profile.Status = "Honorbuddy isn't responding. restarting";
                        }
                        else// otherwise nicely close the window instead so it can logout serverside.
                        {
                            Profile.Log("Honorbuddy has crashed.. So lets restart it");
                            Profile.Status = "Honorbuddy has crashed. restarting";
                        }
                        Stop();
                    }
                }
            }
        }

        public void Stop()
        {
            // try to aquire lock, if fail then kill process anyways.
            bool lockAquried = Monitor.TryEnter(_lockObject, 500);
            if (IsRunning)
            {
                if (BotProcess != null && !BotProcess.HasExited)
                    CloseBotProcess();
                IsRunning = false;
                StartupSequenceIsComplete = false;
            }
            if (lockAquried) // release lock if it was aquired
                Monitor.Exit(_lockObject);
        }


        public void SetStartupSequenceToComplete()
        {
            StartupSequenceIsComplete = true;
            if (HbRelogManager.Settings.MinimizeHbOnStart)
                NativeMethods.ShowWindow(BotProcess.MainWindowHandle, NativeMethods.ShowWindowCommands.Minimize);
            if (OnStartupSequenceIsComplete != null)
                OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
        }

        readonly Stopwatch _hbRespondingSw = new Stopwatch();
        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        public bool HBIsResponding
        {
            get
            {
                if (!HbRelogManager.Settings.CheckHbResponsiveness)
                    return true;
                if (BotProcess != null && !BotProcess.HasExited && !BotProcess.Responding && StartupSequenceIsComplete)
                {
                    if (!_hbRespondingSw.IsRunning)
                        _hbRespondingSw.Start();
                    if (_hbRespondingSw.ElapsedMilliseconds >= 20000)
                        return false;
                }
                else if (_hbRespondingSw.IsRunning)
                    _hbRespondingSw.Start();
                return true;
            }
        }

        DateTime _crashTimeStamp = DateTime.Now;
        public bool HBHasCrashed
        {
            get
            {
                // check for crash every 10 seconds
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    _crashTimeStamp = DateTime.Now;
                    List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(BotProcess.Id);
                    string hbName = Path.GetFileNameWithoutExtension(Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                    return childWinHandles.Select(NativeMethods.GetWindowText).
                        Count(n => !string.IsNullOrEmpty(n) && (n == "Honorbuddy" ||
                            (hbName != "Honorbuddy" && n.Contains(hbName)))) > 1;
                }
                return false;
            }
        }

        static public class HBStartupManager
        {
            static readonly object LockObject = new object();
            static readonly Dictionary<string, DateTime> TimeStamps = new Dictionary<string, DateTime>();
            static public bool CanStart(string path)
            {
                string key = path.ToUpper();
                lock (LockObject)
                {
                    if (TimeStamps.ContainsKey(key) &&
                        DateTime.Now - TimeStamps[key] < TimeSpan.FromSeconds(HbRelogManager.Settings.HBDelay))
                    {
                        return false;
                    }
                    TimeStamps[key] = DateTime.Now;
                }
                return true;
            }
        }
    }
}
