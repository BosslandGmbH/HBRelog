using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class ScanOffsetsState : State
    {
        private readonly WowManager _wowManager;

        public ScanOffsetsState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 900; }
        }

        public override bool NeedToRun
        {
            get
            {
                return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe())
					&& !_wowManager.StartupSequenceIsComplete && _wowManager.Memory != null &&
                       (string.IsNullOrEmpty(HbRelogManager.Settings.WowVersion)
                       || !HbRelogManager.Settings.WowVersion.Equals(_wowManager.GameProcess.VersionString())
                       || HbRelogManager.Settings.GlueStateOffset == 0
                       || HbRelogManager.Settings.GameStateOffset == 0
                       || HbRelogManager.Settings.FocusedWidgetOffset == 0
                       || HbRelogManager.Settings.LuaStateOffset == 0);
            }
        }

        public override void Run()
        {
            var versionString = _wowManager.GameProcess.VersionString();
            HbRelogManager.Settings.GameStateOffset = (uint)WowPatterns.GameStatePattern.Find(_wowManager.Memory);
            Log.Debug("GameState Offset found at 0x{0:X}", HbRelogManager.Settings.GameStateOffset);

            HbRelogManager.Settings.LuaStateOffset = (uint)WowPatterns.LuaStatePattern.Find(_wowManager.Memory);
            Log.Debug("LuaState Offset found at 0x{0:X}", HbRelogManager.Settings.LuaStateOffset);

            HbRelogManager.Settings.FocusedWidgetOffset = (uint)WowPatterns.FocusedWidgetPattern.Find(_wowManager.Memory);
            Log.Debug("FocusedWidget Offset found at 0x{0:X}", HbRelogManager.Settings.FocusedWidgetOffset);

            HbRelogManager.Settings.GlueStateOffset = (uint)WowPatterns.GlueStatePattern.Find(_wowManager.Memory);
            Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HbRelogManager.Settings.GlueStateOffset);

            HbRelogManager.Settings.WowVersion = versionString;
            HbRelogManager.Settings.Save();
        }
    }
}