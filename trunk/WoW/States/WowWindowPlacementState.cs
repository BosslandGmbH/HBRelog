using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

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
                return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe()) 
						&& !_wowManager.StartupSequenceIsComplete 
						&& !_wowManager.InGame 
						&& !_wowManager.IsConnectiongOrLoading 
						&& !_wowManager.ProcessIsReadyForInput;
			}
        }

        public override void Run()
        {
	        if (HbRelogManager.Settings.SetGameWindowTitle)
	        {
			    var title = HbRelogManager.Settings.GameWindowTitle;

			    var profileNameI = title.IndexOf("{name}", StringComparison.InvariantCultureIgnoreCase);

			    if (profileNameI >= 0)
				    title = title.Replace(title.Substring(profileNameI, "{name}".Length),
								_wowManager.Profile.Settings.ProfileName);

				var pidI = title.IndexOf("{pid}", StringComparison.InvariantCultureIgnoreCase);
				if (pidI >= 0)
					title = title.Replace(title.Substring(pidI, "{pid}".Length),
								_wowManager.GameProcess.Id.ToString(CultureInfo.InvariantCulture));
		        	
				// change window title
				NativeMethods.SetWindowText(_wowManager.GameProcess.MainWindowHandle, title);	        
	        }


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