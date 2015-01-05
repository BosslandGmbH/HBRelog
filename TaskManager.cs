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
using System.Collections.ObjectModel;
using System.Linq;
using HighVoltz.HBRelog.Honorbuddy;
using HighVoltz.HBRelog.Tasks;
using HighVoltz.HBRelog.WoW;

namespace HighVoltz.HBRelog
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
		public BMTask CurrentTask { get; private set; }

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
	            PulseTasks();
            }
        }


	    private void PulseTasks()
	    {
		    if (!Tasks.Any())
			    return;

			// reset tasks if they're all complete
			if (Tasks.All(t => t.IsDone))
			{
				foreach (var task in Tasks)
					task.Reset();
			}

			// get the 1st task that isn't done and pulse it.
			CurrentTask = Tasks.FirstOrDefault(t => !t.IsDone);
			if (CurrentTask != null)
			{
				if (!CurrentTask.IsRunning)
					CurrentTask.Start();
				CurrentTask.Pulse();
				if (CurrentTask is WaitTask && CurrentTask.IsRunning)
					Profile.TaskTooltip = CurrentTask.ToolTip;
				else if (!string.IsNullOrEmpty(Profile.TaskTooltip))
					Profile.TaskTooltip = null;
			}
	    }

        public void Start()
        {
            // display tasks in log for debugin purposes
            if (!StartupSequenceIsComplete)
            {
                Profile.Log("********* Tasks ***********");
                foreach (var task in Profile.Tasks)
                {
                    // the tooltip for Logon Task can contain character name so lets just print the name of task to log instead.
                    if (task is LogonTask)
                        Profile.Log(task.Name);
                    else
                        Profile.Log(task.ToolTip);
                }
                Profile.Log("********* End of Task list ***********");
            }
            // check if idle is 1st task.
            bool idleIs1stTask = Profile.Tasks.Count > 0 && Profile.Tasks[0] is IdleTask;
            if (!WowManager.IsRunning && !idleIs1stTask)
            {
                WowManager.SetSettings(Profile.Settings.WowSettings);
                WowManager.Start();
            }
            else if (idleIs1stTask)
                StartupSequenceIsComplete = true;
            IsRunning = true;
        }

        public void Stop()
        {
            Profile.Status = "Stopped";
            StartupSequenceIsComplete = false;
            WowManager.Stop();
            HonorbuddyManager.Stop();
            foreach (var task in Tasks)
                task.Reset();
            IsRunning = false;
        }
    }
}
