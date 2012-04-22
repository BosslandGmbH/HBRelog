using System;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog.Remoting
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    class RemotingApi : MarshalByRefObject, IRemotingApi
    {
        CharacterProfile GetProfileByHbProcID(int hbProcID)
        {
            return HbRelogManager.Settings.CharacterProfiles.
                    FirstOrDefault(p => p.TaskManager.HonorbuddyManager.BotProcess != null &&
                    p.TaskManager.HonorbuddyManager.BotProcess.Id == hbProcID);
        }

        CharacterProfile GetProfileByName(string name)
        {
            return HbRelogManager.Settings.CharacterProfiles.
                    FirstOrDefault(p => p.Settings.ProfileName.
                        Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        public bool Init(int hbProcID)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
            {
                profile.TaskManager.HonorbuddyManager.SetStartupSequenceToComplete();
                return true;
            }
            return false;
        }

        public void RestartHB(int hbProcID)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
            {
                profile.Status = "Restarting Honorbuddy";
                var botProc = profile.TaskManager.HonorbuddyManager.BotProcess;
                if (botProc != null && !botProc.HasExited)
                    botProc.CloseMainWindow();
            }
        }

        public void RestartWow(int hbProcID)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
            {
                profile.Status = "Restarting WoW";
                var wowProc = profile.TaskManager.WowManager.GameProcess;
                if (wowProc != null && !wowProc.HasExited)
                    wowProc.Kill();
            }
        }

        public string[] GetProfileNames()
        {
            return (from profile in HbRelogManager.Settings.CharacterProfiles
                    select profile.Settings.ProfileName).ToArray();
        }

        public string GetCurrentProfileName(int hbProcID)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            return profile != null ? profile.Settings.ProfileName : string.Empty;
        }

        public void StartProfile(string profileName)
        {
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null)
                profile.Start();
        }

        public void StopProfile(string profileName)
        {
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null)
                profile.Stop();
        }

        public void PauseProfile(string profileName)
        {
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null)
                profile.Pause();
        }

        public void IdleProfile(string profileName, TimeSpan time)
        {
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null && profile.IsRunning)
            {
                profile.Status = "Idle";
                profile.Stop();
                _idleTime = new Timer(IdleTimerCallback, profile, time, TimeSpan.Zero);
            }
        }

// ReSharper disable NotAccessedField.Local
        private Timer _idleTime;
// ReSharper restore NotAccessedField.Local
        void IdleTimerCallback(object profile)
        {
            ((CharacterProfile)profile).Start();
        }

        public void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
            {
                var wowSettings = profile.Settings.WowSettings.ShadowCopy();
                var hbSettings = profile.Settings.HonorbuddySettings.ShadowCopy();

                if (!string.IsNullOrEmpty(character))
                    wowSettings.CharacterName = character;
                if (!string.IsNullOrEmpty(server))
                    wowSettings.ServerName = server;

                if (!string.IsNullOrEmpty(botBase))
                    hbSettings.BotBase = botBase;
                if (!string.IsNullOrEmpty(profilePath))
                    hbSettings.HonorbuddyProfile = profilePath;
                if (!string.IsNullOrEmpty(customClass))
                    hbSettings.CustomClass = customClass;
                profile.Log("Logging on different character.");
                profile.Status = "Logging on a different character";
                // exit wow and honorbuddy
                profile.TaskManager.HonorbuddyManager.Stop();
                profile.TaskManager.WowManager.Stop();
                // assign new settings
                profile.TaskManager.HonorbuddyManager.SetSettings(hbSettings);
                profile.TaskManager.WowManager.SetSettings(wowSettings);
                profile.TaskManager.WowManager.Start();
            }
        }

        // 0 unknown,1 Paused, 2 Running, 3 Stopped
        public int GetProfileStatus(string profileName)
        {
            int status = 0;
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null)
            {
                if (profile.IsPaused)
                    status = 1;
                else if (profile.IsRunning)
                    status = 2;
                else
                    status = 3;
            }
            return status;
        }

        public void SetProfileStatusText(int hbProcID, string status)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
                profile.Status = status;
        }

        public void SetBotInfoToolTip(int hbProcID, string tooltip)
        {
            CharacterProfile profile = GetProfileByHbProcID(hbProcID);
            if (profile != null)
                profile.BotInfoTooltip = tooltip;
        }



        public void SkipCurrentTask(string profileName)
        {
            CharacterProfile profile = GetProfileByName(profileName);
            if (profile != null)
            {
                BMTask currentTask = profile.Tasks.FirstOrDefault(t => !t.IsDone);
                if (currentTask != null)
                    currentTask.Stop();
            }
        }
    }
}
