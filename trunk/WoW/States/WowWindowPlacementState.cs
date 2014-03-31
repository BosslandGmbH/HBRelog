using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;
using Test.Lua;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class WowWindowPlacementState : State
    {
        private readonly WowManager _wowManager;

        public WowWindowPlacementState(WowManager wowManager)
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
				return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExited) 
						&& !_wowManager.StartupSequenceIsComplete 
						&& !_wowManager.InGame 
						&& !_wowManager.IsConnectiongOrLoading 
						&& !_wowManager.ProcessIsReadyForInput;
			}
        }

        public override void Run()
        {
            // change window title
            NativeMethods.SetWindowText(
                _wowManager.GameProcess.MainWindowHandle, string.Format("{0} - ProcID: {1}", _wowManager.Profile.Settings.ProfileName, _wowManager.GameProcess.Id));

            // resize and position window.
            if (_wowManager.Settings.WowWindowWidth > 0 && _wowManager.Settings.WowWindowHeight > 0)
            {
                _wowManager.Profile.Log(
                    "Setting Window location to X:{0}, Y:{1} and Size to Width {2}, Height:{3}",
                    _wowManager.Settings.WowWindowX,
                    _wowManager.Settings.WowWindowY,
                    _wowManager.Settings.WowWindowWidth,
                    _wowManager.Settings.WowWindowHeight);

                Utility.ResizeAndMoveWindow(
                    _wowManager.GameProcess.MainWindowHandle,
                    _wowManager.Settings.WowWindowX,
                    _wowManager.Settings.WowWindowY,
                    _wowManager.Settings.WowWindowWidth,
                    _wowManager.Settings.WowWindowHeight);
            }
            _wowManager.ProcessIsReadyForInput = true;
        }
    }
}