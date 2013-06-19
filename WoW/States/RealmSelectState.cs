using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class RealmSelectState : State
    {
        private readonly WowManager _wowManager;

        public RealmSelectState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 600; }
        }

        public override bool NeedToRun
        {
            get { return !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && !_wowManager.IsConnectiongOrLoading && _wowManager.GlueStatus == WowManager.GlueState.ServerSelection; }
        }

        public override void Run()
        {
            if (_wowManager.Throttled)
                return;
        }
    }
}