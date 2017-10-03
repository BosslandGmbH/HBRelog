using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class MonitorState : State
    {
        private readonly WowManager _wowManager;

        public MonitorState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 100; }
        }


        public override bool NeedToRun
        {
            get
            {
                return _wowManager.IsConnectingOrLoading || _wowManager.InGame; 
            }
        }

        public override void Run()
        {
            if (_wowManager.LockToken.IsValid)
                _wowManager.LockToken.ReleaseLock();

            // The HBRelogHelper plugin will monitor the health of the WoW process
            if (_wowManager.Memory != null)
            {
                _wowManager.Memory.Dispose();
                _wowManager.Memory = null;
            }

            if (!_wowManager.StartupSequenceIsComplete)
            {
                _wowManager.SetStartupSequenceToComplete();                
            }
        }
    }
}