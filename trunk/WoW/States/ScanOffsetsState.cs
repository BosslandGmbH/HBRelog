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
            get { return 800; }
        }

        public override bool NeedToRun
        {
            get
            {
                return !_wowManager.StartupSequenceIsComplete &&
                       (string.IsNullOrEmpty(HbRelogManager.Settings.WowVersion) || !HbRelogManager.Settings.WowVersion.Equals(_wowManager.GameProcess.VersionString()));
            }
        }

        public override void Run()
        {
            if (_wowManager.Memory != null)
            {
                HbRelogManager.Settings.GameStateOffset = (uint)WoWPatterns.GameStatePattern.Find(_wowManager.Memory);
                Log.Debug("GameState Offset found at 0x{0:X}", HbRelogManager.Settings.GameStateOffset);

                HbRelogManager.Settings.LuaStateOffset = (uint)WoWPatterns.LuaStatePattern.Find(_wowManager.Memory);
                Log.Debug("LuaState Offset found at 0x{0:X}", HbRelogManager.Settings.LuaStateOffset);

                HbRelogManager.Settings.FocusedWidgetOffset = (uint)WoWPatterns.FocusedWidgetPattern.Find(_wowManager.Memory);
                Log.Debug("FocusedWidget Offset found at 0x{0:X}", HbRelogManager.Settings.FocusedWidgetOffset);

                HbRelogManager.Settings.GlueStateOffset = (uint)WoWPatterns.GlueStatePattern.Find(_wowManager.Memory);
                Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HbRelogManager.Settings.GlueStateOffset);

                HbRelogManager.Settings.WowVersion = _wowManager.GameProcess.VersionString();
                HbRelogManager.Settings.Save();
            }
            else
                MessageBox.Show("Can not scan for offsets before attaching to process");
        }
    }
}