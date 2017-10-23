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
using System.Threading.Tasks;
using System.Windows;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.FiniteStateMachine.FiniteStateMachine;
using HighVoltz.HBRelog.Honorbuddy.States;
using HighVoltz.HBRelog.Settings;
using HighVoltz.HBRelog.WoW;
using HighVoltz.HBRelog.WoW.States;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32.SafeHandles;
using MonitorState = HighVoltz.HBRelog.Honorbuddy.States.MonitorState;
using System.Text.RegularExpressions;

namespace HighVoltz.HBRelog.Honorbuddy
{
    public class HonorbuddyManager : Engine, IBotManager
    {
        private Stopwatch _botExitTimer;
        readonly object _lockObject = new object();
        private Process _launcherProc;


        public bool StartupSequenceIsComplete { get; private set; }

        // Note: if you're getting a compile error here then you need to install VS 2015 or better.
        public Stopwatch LastHeartbeat { get; } = new Stopwatch();

        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        CharacterProfile _profile;
        public CharacterProfile Profile
        {
            get { return _profile; }
            private set
            {
                _profile = value;
                Settings = value.Settings.HonorbuddySettings;
            }
        }

        public HonorbuddySettings Settings { get; private set; }
        public Process BotProcess { get; private set; }

	    public bool WaitForBotToExit
	    {
		    get
		    {
                if (Profile.TaskManager.WowManager.StartupSequenceIsComplete)
					return false;

                if (BotProcess == null || BotProcess.HasExitedSafe())
					return false;
			    if (_botExitTimer == null)
				    _botExitTimer = Stopwatch.StartNew();
			    return _botExitTimer.ElapsedMilliseconds < 20000;
		    }
	    }

        public HonorbuddyManager(CharacterProfile profile)
        {
            Profile = profile;
            States = new List<State> 
            {
                new UpdateHonorbuddyState(this),
                new StartHonorbuddyState(this),
                new MonitorState(this),
            };
        }

        public void SetSettings(HonorbuddySettings settings)
        {
            Settings = settings;
        }

        bool _isExiting;
        public void CloseBotProcess()
        {
            if (!_isExiting && BotProcess != null && !BotProcess.HasExitedSafe())
            {
                _isExiting = true;
                Task.Run(async () => await Utility.CloseBotProcessAsync(BotProcess, Profile))
                    .ContinueWith(o =>
                    {
                        _isExiting = false;
                        BotProcess.Dispose();
                        BotProcess = null;
                        if (o.IsFaulted)
                            Profile.Log("{0}", o.Exception.Flatten().ToString());
                    });
            }
        }

        public void Start()
        {
            IsRunning = true;
        }


        internal void StartHonorbuddy()
        {
            // check if a batch file or any .exe besides Honorbuddy.exe is used and try to get the child HB process started by this process.

            if (_launcherProc != null)
            {
                Process botProcess;
                // Two methods are used to find the HB process if a launcher is used;
                // Method one: launcher exit code return the HB process ID or a negative if an error occured. 
                //			   If launcher does not use return the expected return values then a batch file or console app
                //			   must be used to start the launcher and return the expected return codes.	
                // Method two: Find a child WoW process of the launcher process.

                if (_launcherProc.HasExited && _launcherProc.ExitCode < 0)
                {
                    Profile.Log("Pausing profile because HB launcher exited with error code: {0}", _launcherProc.ExitCode);
                    Profile.Pause();
                    return;
                }

                if (!_launcherProc.HasExited || _launcherProc.ExitCode == 0
                                || !Utility.TryGetProcessById(_launcherProc.ExitCode, out botProcess))
                {
                    var executablePath = Path.GetFileNameWithoutExtension(Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                    botProcess = Utility.GetChildProcessByName(_launcherProc.Id, "Honorbuddy")
                        ?? Utility.GetChildProcessByName(_launcherProc.Id, executablePath); // Renamed executables
                }

                if (botProcess == null)
                {
                    Profile.Log("Waiting on external application to start Honorbuddy");
                    Profile.Status = "Waiting on external application to start Honorbuddy";
                    return;
                }

                Profile.Log($"HB Launcher Pid: {_launcherProc.Id} launched HB Pid: {botProcess.Id}");
                _launcherProc.Dispose();
                _launcherProc = null;
                BotProcess = botProcess;
                return;
            }

            bool launchingHB = IsHonorbuddyPath(Profile.Settings.HonorbuddySettings.HonorbuddyPath);

            _botExitTimer = null;
            Profile.Log($"Starting {Profile.Settings.HonorbuddySettings.HonorbuddyPath}");
            Profile.Status = "Starting Honorbuddy";
            StartupSequenceIsComplete = false;

            string hbArgs = "";

            if (launchingHB)
            {
                hbArgs = "/noupdate " +
                    $"/pid={Profile.TaskManager.WowManager.GameProcessId} " +
                    "/autostart " +
                    $"{(!string.IsNullOrEmpty(Settings.HonorbuddyKey) ? $"/hbkey=\"{Settings.HonorbuddyKey}\" " : string.Empty)}" +
                    $"{(!string.IsNullOrEmpty(Settings.CustomClass) ? $"/customclass=\"{Settings.CustomClass}\" " : string.Empty)}" +
                    $"{(!string.IsNullOrEmpty(Settings.HonorbuddyProfile) ? $"/loadprofile=\"{Settings.HonorbuddyProfile}\" " : string.Empty)}" +
                    $"{(!string.IsNullOrEmpty(Settings.BotBase) ? $"/botname=\"{Settings.BotBase}\" " : string.Empty)}";
            }

            if (!string.IsNullOrEmpty(Settings.HonorbuddyArgs))
		        hbArgs +=  Settings.HonorbuddyArgs.Trim();

            var hbWorkingDirectory = Path.GetDirectoryName(Settings.HonorbuddyPath);
            var procStartI = new ProcessStartInfo(Settings.HonorbuddyPath, hbArgs)
            {
                WorkingDirectory = hbWorkingDirectory
            };


            if (launchingHB)
            {
                procStartI.UseShellExecute = false;
                procStartI.RedirectStandardOutput = true;
                var proc = Process.Start(procStartI);
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                proc.Dispose();
                int pid = int.Parse(Regex.Match(output, @"PID (?<id>[0-9]+)").Groups["id"].Value);
                BotProcess = Process.GetProcessById(pid);
                if (BotProcess != null && Launcher.Helpers.IsUacEnabled)
                {
                    var path = BotProcess.MainModule.FileName;
                }
            }
            else
            {
                _launcherProc = Process.Start(procStartI);
            }

            HbStartupTimeStamp = DateTime.Now;
        }

        public DateTime HbStartupTimeStamp { get; private set; }

        public override void Pulse()
        {
            lock (_lockObject)
            {
                base.Pulse();
            }
        }

        public void Stop()
        {
            // try to aquire lock, if fail then kill process anyways.
            bool lockAquried = Monitor.TryEnter(_lockObject, 500);
            if (IsRunning)
            {
                if (BotProcess != null)
                {
                    if (!BotProcess.HasExitedSafe())
                    {
                        CloseBotProcess();
                    }
                    else
                    {
                        BotProcess.Dispose();
                        BotProcess = null;
                    }
                }

                LastHeartbeat.Reset();
                IsRunning = false;
                StartupSequenceIsComplete = false;
            }
            if (lockAquried) // release lock if it was aquired
                Monitor.Exit(_lockObject);
        }


        public void SetStartupSequenceToComplete()
        {
            StartupSequenceIsComplete = true;
            LastHeartbeat.Restart();

            OnStartupSequenceIsComplete?.Invoke(this, new ProfileEventArgs(Profile));
        }

        private bool IsHonorbuddyPath(string path)
        {
            var originalExeFileName = FileVersionInfo.GetVersionInfo(path).OriginalFilename;
            return originalExeFileName == "Honorbuddy.exe";
        }

        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        internal static class HBStartupManager
        {
            private static readonly object LockObject = new object();
            private static readonly Dictionary<string, DateTime> TimeStamps =
                new Dictionary<string, DateTime>();

            public static bool CanStart(string path)
            {
                var key = path.ToUpper();
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
