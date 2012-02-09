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
using System.Text;
using HighVoltz.Settings;

namespace HighVoltz.Honorbuddy
{
    public class HonorbuddyManager : IBotManager
    {
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

        public void Start()
        {
            if (File.Exists(Settings.HonorbuddyPath))
            {
                // remove internet zone restrictions from Honorbuddy.exe if it exists
                Utility.UnblockFileIfZoneRestricted(Settings.HonorbuddyPath);
                IsRunning = true;
                StartHonorbuddy();
            }
            else
                throw new InvalidOperationException(string.Format("path to honorbuddy.exe does not exist: {0}", Settings.HonorbuddyPath));
        }

        bool _waitingToStart = false;

        void StartHonorbuddy()
        {
            _waitingToStart = true;
        }

        public void Pulse()
        {
            if (IsRunning)
            {
                if (_waitingToStart)
                {  // we need to delay starting honorbuddy for a few seconds if another instance from same path was started a few seconds ago
                    if (HBStartupManager.CanStart(Settings.HonorbuddyPath))
                    {
                        Profile.Log("starting {0}", Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                        Profile.Status = "Starting Honorbuddy";
                        StartupSequenceIsComplete = false;
                        string hbArgs = string.Format("/pid={0} /autostart {1}{2}{3}",
                            Profile.TaskManager.WowManager.GameProcess.Id,
                            !string.IsNullOrEmpty(Settings.CustomClass) ? string.Format("/customclass=\"{0}\" ", Settings.CustomClass) : string.Empty,
                            !string.IsNullOrEmpty(Settings.HonorbuddyPath) ? string.Format("/loadprofile=\"{0}\" ", Settings.HonorbuddyProfile) : string.Empty,
                            !string.IsNullOrEmpty(Settings.BotBase) ? string.Format("/botname=\"{0}\" ", Settings.BotBase) : string.Empty
                            );
                        BotProcess = Process.Start(Settings.HonorbuddyPath, hbArgs);
                        _waitingToStart = false;
                    }
                    else
                        return;
                }
                // restart wow hb if it has exited
                if (BotProcess.HasExited)
                {
                    Profile.Log("Honorbuddy process was terminated. Restarting");

                    Profile.Status = "Honorbuddy has exited.";
                    StartupSequenceIsComplete = false;
                    IsRunning = false;
                    return;
                }
                // return if hb isn't ready for input.
                if (!BotProcess.WaitForInputIdle(0))
                    return;

                if (!StartupSequenceIsComplete && NativeMethods.GetWindowText(BotProcess.MainWindowHandle).Contains("ID"))
                {
                    string hbName = Path.GetFileNameWithoutExtension(Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                    if (NativeMethods.GetWindowText(BotProcess.MainWindowHandle).Contains(hbName))
                    {
                        StartupSequenceIsComplete = true;
                        if (OnStartupSequenceIsComplete != null)
                            OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
                    }
                }
                if (!HBIsResponding || HBHasCrashed)
                {
                    if (!HBIsResponding) // we need to kill the process if it's not responding. 
                        BotProcess.Kill();
                    else// otherwise nicely close the window instead so it can logout serverside.
                        BotProcess.CloseMainWindow();
                    Profile.Log("Honorbuddy has crashed.. So lets restart it");
                    Profile.Status = "Honorbuddy has crashed. restarting";
                    StartupSequenceIsComplete = false;
                    IsRunning = false;
                }
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                if (BotProcess != null)
                    BotProcess.CloseMainWindow();
                BotProcess = null;
                IsRunning = false;
                StartupSequenceIsComplete = false;
            }
        }

        Stopwatch _hbRespondingSW = new Stopwatch();
        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        public bool HBIsResponding
        {
            get
            {
                bool isResponding = BotProcess.Responding;
                if (!isResponding && !_hbRespondingSW.IsRunning)
                    _hbRespondingSW.Start();
                if (_hbRespondingSW.ElapsedMilliseconds >= 10000 && !isResponding)
                    return false;
                else if (isResponding && _hbRespondingSW.IsRunning)
                    _hbRespondingSW.Reset();
                return true;
            }
        }

        DateTime _crashTimeStamp = DateTime.Now;
        public bool HBHasCrashed
        {
            get
            {
                // check for crash every 10 seconds and cache the result
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    _crashTimeStamp = DateTime.Now;
                    List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(BotProcess.Id);
                    string hbName = Path.GetFileNameWithoutExtension(Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                    return childWinHandles.Count(h => NativeMethods.GetWindowText(h) == hbName) > 1;
                }
                return false;
            }
        }

        static public class HBStartupManager
        {
            static Dictionary<string, DateTime> TimeStamps = new Dictionary<string, DateTime>();
            static public bool CanStart(string path)
            {
                string key = path.ToUpper();
                if (TimeStamps.ContainsKey(key))
                {
                    if (DateTime.Now - TimeStamps[key] < TimeSpan.FromSeconds(10))
                        return false;
                    else
                        TimeStamps.Remove(key);
                }
                else
                    TimeStamps[key] = DateTime.Now;
                return true;
            }
        }
    }
}
