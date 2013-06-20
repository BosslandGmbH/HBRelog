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
            string reason = string.Empty;
            if (_wowManager.LockToken == null || !_wowManager.LockToken.IsValid)
                _wowManager.LockToken = WowLockToken.RequestLock(_wowManager, out reason);

            if (_wowManager.LockToken == null)
            {
                _wowManager.Profile.Status = reason;
                return;
            }
            if (_wowManager.ServerIsOnline)
                _wowManager.LockToken.StartWoW();
            else
            {
                _wowManager.Profile.Status = string.Format("{0} is offline", _wowManager.Settings.ServerName);
                _wowManager.Profile.Log("Server is offline");
            }
        }

    }
}
