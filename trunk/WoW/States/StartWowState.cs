using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.WoW.States
{
    class StartWowState : State
    {
        private readonly WowManager _wowManager;

        public StartWowState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 1000; }
        }

        public override bool NeedToRun
        {
            get { return _wowManager.GameProcess == null || _wowManager.GameProcess.HasExited; }
        }

        public override void Run()
        {
            var waitingToStart = !WowManager.WowStartupManager.CanStart(_wowManager.Settings.WowPath);

            if (waitingToStart)
            {
                _wowManager.Profile.Status = "Waiting to start";
            }
            else
            {
                _wowManager.Profile.Log("WoW process was terminated. Restarting");
                _wowManager.Profile.Status = "WoW process was terminated. Restarting";
            }
            if (waitingToStart)
                return;
            _wowManager.Profile.Log("Starting {0}", _wowManager.Settings.WowPath);
            _wowManager.Profile.Status = "Starting WoW";
            //_wowManager.StartupSequenceIsComplete = false;
            _wowManager.Memory = null;
            // start wow maximized to reduce chance of mis-clicking UI elements or having the window clipped off edge of screen.
            var pi = new ProcessStartInfo(_wowManager.Settings.WowPath, _wowManager.Settings.WowArgs) ;
            _wowManager.GameProcess = Process.Start(pi);
            _wowManager.ProcessIsReadyForInput = false;
            _wowManager.LoginTimer.Reset();
            if (_wowManager.IsUsingLauncher)
            {
                _wowManager.LauncherPid = _wowManager.GameProcess.Id;
                // set GameProcess temporarily to HBRelog because laucher can exit before wow starts 
                _wowManager.GameProcess = Process.GetCurrentProcess();
            }
        }

    }
}
