using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    class StartHonorbuddyState : State
    {
        #region Fields

        private readonly HonorbuddyManager _hbManager;
        #endregion

        #region Constructors

        public StartHonorbuddyState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion


        #region State Members

        public override int Priority
        {
            get { return 800; }
        }

        public override bool NeedToRun
        {
            get { return _hbManager.Profile.IsRunning && _hbManager.BotProcess == null  && _hbManager.Profile.TaskManager.WowManager.GameProcess != null && !_hbManager.Profile.TaskManager.WowManager.GameProcess.HasExited; }
        }

        public override void Run()
        {
            // remove internet zone restrictions from Honorbuddy.exe if it exists
            Utility.UnblockFileIfZoneRestricted(_hbManager.Settings.HonorbuddyPath);
            // we need to delay starting honorbuddy for a few seconds if another instance from same path was started a few seconds ago
            if (HonorbuddyManager.HBStartupManager.CanStart(_hbManager.Settings.HonorbuddyPath))
                _hbManager.StartHonorbuddy();

        }

        #endregion
    }
}
