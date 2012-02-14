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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using HighVoltz.Honorbuddy;
using HighVoltz.Tasks;
using HighVoltz.WoW;

namespace HighVoltz
{
    sealed public class TaskManager : IManager
    {
        public readonly ObservableCollection<BMTask> Tasks;
        public readonly WowManager WowManager;
        public readonly HonorbuddyManager HonorbuddyManager;
        public CharacterProfile Profile { get; set; }
        public bool StartupSequenceIsComplete { get; private set; }
        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        public bool IsRunning { get; private set; }

        public TaskManager(CharacterProfile profile)
        {
            Profile = profile;
            WowManager = new WowManager(profile);
            HonorbuddyManager = new HonorbuddyManager(profile);
            Tasks = profile.Tasks;
            StartupSequenceIsComplete = IsRunning = false;
            HonorbuddyManager.OnStartupSequenceIsComplete += HonorbuddyManager_OnStartupSequenceIsComplete;
        }

        void HonorbuddyManager_OnStartupSequenceIsComplete(object sender, ProfileEventArgs e)
        {
            Profile.Log(" WoW and HB startup sequence complete");
            Profile.Status = "Running";
            if (!StartupSequenceIsComplete)
            {
                StartupSequenceIsComplete = true;
                if (OnStartupSequenceIsComplete != null)
                    OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
            }
        }

        public void Pulse()
        {
            if (WowManager.IsRunning)
            {
                WowManager.Pulse();
                if (WowManager.InGame && !HonorbuddyManager.IsRunning)
                {
                    if (!StartupSequenceIsComplete)
                        HonorbuddyManager.SetSettings(Profile.Settings.HonorbuddySettings);
                    HonorbuddyManager.Start();
                }
                if (HonorbuddyManager.IsRunning)
                    HonorbuddyManager.Pulse();
            }
            // only pulse tasks if StartupSequenceIsComplete is true.
            if (StartupSequenceIsComplete)
            {
                // reset tasks if they're all complete
                if (Tasks.Count > 0 && !Tasks.Any(t => !t.IsDone))
                {
                    foreach (var task in Tasks)
                        task.Reset();
                }
                // get the 1st task that isn't done and pulse it.
                BMTask currentTask = Tasks.FirstOrDefault(t => !t.IsDone);
                if (currentTask != null)
                    currentTask.Pulse();
            }
        }

        public void Start()
        {
            if (!WowManager.IsRunning)
            {
                WowManager.SetSettings(Profile.Settings.WowSettings);
                WowManager.Start();
            }
            IsRunning = true;
        }

        public void Stop()
        {
            Profile.Status = "Stopped";
            StartupSequenceIsComplete = false;
            HonorbuddyManager.Stop();
            WowManager.Stop();
            IsRunning = false;
        }
    }
}
