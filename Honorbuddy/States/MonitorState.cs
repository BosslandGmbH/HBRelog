using HighVoltz.HBRelog.FiniteStateMachine;
using System;
using System.Diagnostics;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    internal class MonitorState : State
    {
        #region Fields

        private readonly HonorbuddyManager _hbManager;

        #endregion Fields

        #region Constructors

        public MonitorState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion Constructors

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
                if (_hbManager.BotProcess != null && GetExitCodeSafe(_hbManager.BotProcess) == (int)ExitCode.StopMonitoring)
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
            if (!_hbManager.Profile.TaskManager.WowManager.StartupSequenceIsComplete)
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


            if (_hbManager.StartupSequenceIsComplete && !HBIsResponding)
            {
                _hbManager.Profile.Log("Honorbuddy is not responding.. So lets restart it");
                _hbManager.Profile.Status = "Honorbuddy isn't responding. restarting";
                _hbManager.Stop();
            }
        }

        #endregion State Members

        private int GetExitCodeSafe(Process proc)
        {
            try
            {
                return proc != null && proc.HasExitedSafe()
                    ? proc.ExitCode : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public bool HBIsResponding
        {
            get
            {
                if (!HbRelogManager.Settings.CheckHbResponsiveness)
                    return true;

                return _hbManager.LastHeartbeat.ElapsedMilliseconds < 50000;
            }
        }

        private enum ExitCode
        {
            StopMonitoring = 12,
        }
    }
}