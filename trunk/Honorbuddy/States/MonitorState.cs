using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    class MonitorState : State
    {
        #region Fields

        private readonly HonorbuddyManager _hbManager;
        readonly Stopwatch _hbRespondingSw = new Stopwatch();
        #endregion

        #region Constructors

        public MonitorState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion


        #region State Members

        public override int Priority
        {
            get { return 700; }
        }

        public override bool NeedToRun
        {
            get { return _hbManager.IsRunning; }
        }

        public override void Run()
        {
            // restart wow hb if it has exited
            if (_hbManager.BotProcess == null || _hbManager.BotProcess.HasExitedSafe())
            {
				// bot process exit code of 12 is used by HB to signal relogers to not restart the bot.
	            if (_hbManager.BotProcess != null && _hbManager.BotProcess.ExitCode == (int)ExitCode.StopMonitoring)
	            {
					_hbManager.Profile.Log("Honorbuddy process has exited with code 12, signaling that it should not be restarted");
					_hbManager.Profile.Status = "Honorbuddy has requested a bot shutdown.";
					_hbManager.Profile.Stop();
				}
	            else
	            {		            
					_hbManager.Profile.Log("Honorbuddy process was terminated. Restarting");
					_hbManager.Profile.Status = "Honorbuddy has exited.";
					_hbManager.Stop();
					return;
				}
            }
			var gameProc = _hbManager.Profile.TaskManager.WowManager.GameProcess;
            if (gameProc == null || gameProc.HasExitedSafe())
			{
				if (!_hbManager.WaitForBotToExit)
					_hbManager.Stop();
				return;
			}
            // return if hb isn't ready for input.
            if (!_hbManager.BotProcess.WaitForInputIdle(0))
                return;
            // force the mainWindow handle to cache before HB auto minimizes to system tray..
	        if (_hbManager.BotProcess.MainWindowHandle == IntPtr.Zero) ;

            // check if it's taking Honorbuddy too long to connect.
            if (!_hbManager.StartupSequenceIsComplete && DateTime.Now - _hbManager.HbStartupTimeStamp > TimeSpan.FromMinutes(2))
            {
                _hbManager.Profile.Log("Closing Honorbuddy because it took too long to attach");
                _hbManager.Stop();
            }
            if (!HBIsResponding || HBHasCrashed)
            {
                if (!HBIsResponding) // we need to kill the process if it's not responding. 
                {
                    _hbManager.Profile.Log("Honorbuddy is not responding.. So lets restart it");
                    _hbManager.Profile.Status = "Honorbuddy isn't responding. restarting";
                }
                else
                {
                    _hbManager.Profile.Log("Honorbuddy has crashed.. So lets restart it");
                    _hbManager.Profile.Status = "Honorbuddy has crashed. restarting";
                }
                _hbManager.Stop();
            }
        }

        #endregion

        public bool HBIsResponding
        {
            get
            {
                if (!HbRelogManager.Settings.CheckHbResponsiveness)
                    return true;
                if (_hbManager.BotProcess != null && !_hbManager.BotProcess.HasExitedSafe() && !_hbManager.BotProcess.Responding && _hbManager.StartupSequenceIsComplete)
                {
                    if (!_hbRespondingSw.IsRunning)
                        _hbRespondingSw.Start();
                    if (_hbRespondingSw.ElapsedMilliseconds >= 120000)
                        return false;
                }
                else if (_hbRespondingSw.IsRunning)
                    _hbRespondingSw.Reset();
                return true;
            }
        }

        DateTime _crashTimeStamp = DateTime.Now;
        public bool HBHasCrashed
        {
            get
            {
                // check for crash every 10 seconds
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    _crashTimeStamp = DateTime.Now;
                    List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(_hbManager.BotProcess.Id);
                    string hbName = Path.GetFileNameWithoutExtension(_hbManager.Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                    var windowTitles = childWinHandles.Select(NativeMethods.GetWindowText);
                    var ret = windowTitles.Count(n => !string.IsNullOrEmpty(n) && (n == "Honorbuddy" ||(hbName != "Honorbuddy" && n.Contains(hbName)))) > 1;
                    return ret;
                }
                return false;
            }
        }

	    enum ExitCode
	    {
		    StopMonitoring = 12,
	    }

    }
}
