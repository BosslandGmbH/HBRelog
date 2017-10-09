using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class CharacterCreationState : State
    {
        private readonly WowManager _wowManager;

        public CharacterCreationState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 300; }
        }

        public override bool NeedToRun
        {
	        get
	        {
                return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe()) 
					&& !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame 
					&& !_wowManager.IsConnectingOrLoading 
					&& _wowManager.GlueScreen == GlueScreen.CharCreate;
	        }
        }

        public override void Run()
        {
            var characterCreateFrame = UIObject.GetUIObjectByName<Frame>(_wowManager, "CharacterCreateFrame");
            if (characterCreateFrame != null && characterCreateFrame.IsVisible)
            {
                Utility.SendBackgroundKey(_wowManager.GameWindow, (char) Keys.Escape, false);
                _wowManager.Profile.Log("Pressing 'esc' key to exit character creation screen");
            }
        }
    }
}