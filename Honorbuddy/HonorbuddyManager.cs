﻿/*
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

namespace HighVoltz.HBRelog.Honorbuddy
{
    public class HonorbuddyManager : Engine, IBotManager
    {
        private Stopwatch _botExitTimer;
        readonly object _lockObject = new object();

        public bool StartupSequenceIsComplete { get; private set; }

        // Note: if you're getting a compile error here then you need to install VS 2015 or better.
        public Stopwatch LastHeartbeat { get; } = new Stopwatch();

        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        CharacterProfile _profile;
        public CharacterProfile Profile
        {
            get { return _profile; }
            private set { _profile = value; Settings = value.Settings.HonorbuddySettings; }
        }
        public HonorbuddySettings Settings { get; private set; }
        public Process BotProcess { get; private set; }

	    public bool WaitForBotToExit
	    {
		    get
		    {
                if (Profile.TaskManager.WowManager.GameProcess != null && !Profile.TaskManager.WowManager.GameProcess.HasExitedSafe())
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
	        _botExitTimer = null;
            Profile.Log("starting {0}", Profile.Settings.HonorbuddySettings.HonorbuddyPath);
            Profile.Status = "Starting Honorbuddy";
            StartupSequenceIsComplete = false;
	        string hbArgs =
		        "/noupdate " +
		        $"/pid={Profile.TaskManager.WowManager.GameProcess.Id} " +
		        "/autostart " +
		        $"{(!string.IsNullOrEmpty(Settings.HonorbuddyKey) ? $"/hbkey=\"{Settings.HonorbuddyKey}\" " : string.Empty)}" +
		        $"{(!string.IsNullOrEmpty(Settings.CustomClass) ? $"/customclass=\"{Settings.CustomClass}\" " : string.Empty)}" +
		        $"{(!string.IsNullOrEmpty(Settings.HonorbuddyProfile) ? $"/loadprofile=\"{Settings.HonorbuddyProfile}\" " : string.Empty)}" +
		        $"{(!string.IsNullOrEmpty(Settings.BotBase) ? $"/botname=\"{Settings.BotBase}\" " : string.Empty)}";

	        if (!string.IsNullOrEmpty(Settings.HonorbuddyArgs))
		        hbArgs +=  Settings.HonorbuddyArgs.Trim();

            var hbWorkingDirectory = Path.GetDirectoryName(Settings.HonorbuddyPath);
            var procStartI = new ProcessStartInfo(Settings.HonorbuddyPath, hbArgs)
            {
                WorkingDirectory = hbWorkingDirectory
            };
            BotProcess = Process.Start(procStartI);
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
                        CloseBotProcess();
                    else
                        BotProcess = null;
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

        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        static internal class HBStartupManager
        {
            private static readonly object LockObject = new object();
            private static readonly Dictionary<string, DateTime> TimeStamps = new Dictionary<string, DateTime>();

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
