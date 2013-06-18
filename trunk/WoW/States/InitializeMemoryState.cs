using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GreyMagic;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.WoW.States
{
    class InitializeMemoryState : State
    {
        private readonly WowManager _wowManager;

        public InitializeMemoryState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 900; }
        }

        public override bool NeedToRun
        {
            get { return _wowManager.Memory == null; }
        }

        public override void Run()
        {
            // check if a batch file or any .exe besides WoW.exe is used and try to get the child WoW process started by this process.
            if (_wowManager.IsUsingLauncher && !Path.GetFileName(_wowManager.GameProcess.MainModule.FileName).Equals("Wow.exe", StringComparison.CurrentCultureIgnoreCase))
            {
                Process wowProcess = null;
                if (_wowManager.LauncherPid > 0)
                {
                    wowProcess = Utility.GetChildProcessByName(_wowManager.LauncherPid, "Wow");
                }
                else
                {
                    // seems like the launcher process terminated early before we could grab the Game process that it started.. 
                    // so we just find the 1st game process that's not monitor by HBRelog.
                    var processes = Process.GetProcessesByName("Wow");
                    foreach (var characterProfile in HbRelogManager.Settings.CharacterProfiles.Where(c => c.IsRunning && !c.IsPaused))
                    {
                        var proc = characterProfile.TaskManager.WowManager.GameProcess;
                        if (proc == null || proc.HasExited || processes.Any(p => p.Id == proc.Id))
                            continue;
                        wowProcess = proc;
                        break;
                    }
                }

                if (wowProcess == null)
                {
                    _wowManager.Profile.Log("Waiting on external application to start WoW");
                    _wowManager.Profile.Status = "Waiting on external application to start WoW";
                    return;
                }
                _wowManager.GameProcess = wowProcess;
            }
            // return if wow isn't ready for input.
            if (!_wowManager.GameProcess.WaitForInputIdle(0))
                return;
            _wowManager.Memory = new ExternalProcessReader(_wowManager.GameProcess);
        }
    }
}
