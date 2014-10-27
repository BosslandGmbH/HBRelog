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
                if (_wowManager.GameProcess == null || _wowManager.GameProcess.HasExitedSafe())
                    return false;

                return _wowManager.StartupSequenceIsComplete || _wowManager.InGame; 
            }
        }

        public override void Run()
        {
			if (_wowManager.LockToken.IsValid)
				_wowManager.LockToken.ReleaseLock();

            if (!_wowManager.StartupSequenceIsComplete)
            {
                _wowManager.SetStartupSequenceToComplete();
                _loggedOutSw.Reset();
                _wowIsLoggedOutForTooLong = false;
            }

            var trouble = FindTrouble();
            if (trouble == WowProblem.None)
                return;

            switch (trouble)
            {
                case WowProblem.Crash:
                    _wowManager.Profile.Status = "WoW has crashed. restarting";
                    _wowManager.Profile.Log("WoW has crashed.. So lets restart WoW");
                    break;
                case WowProblem.Disconnected:
                    _wowManager.Profile.Log("WoW has disconnected.. So lets restart WoW");
                    _wowManager.Profile.Status = "WoW has DCed. restarting";
                    break;
                case WowProblem.Unresponsive:
                    _wowManager.Profile.Status = "WoW is not responding. restarting";
                    _wowManager.Profile.Log("WoW is not responding.. So lets restart WoW");
                    break;
                case WowProblem.LoggedOutForTooLong:
                    _wowManager.Profile.Log("Restarting wow because it was logged out for more than 40 seconds");
                    _wowManager.Profile.Status = "WoW was logged out for too long. restarting";
                    break;
            }
            _wowManager.CloseGameProcess();
        }

        private WowProblem FindTrouble()
        {
            if (!_wowManager.StartupSequenceIsComplete)
                return WowProblem.None;

            if (_wowManager.GlueStatus == WowManager.GlueState.Disconnected)
                return WowProblem.Disconnected;

            if (WowIsLoggedOutForTooLong)
                return WowProblem.LoggedOutForTooLong;

            if (HbRelogManager.Settings.CheckWowResponsiveness && WowIsUnresponsive)
                return WowProblem.Unresponsive;

            if (WowHasCrashed)
                return WowProblem.Crash;

            return WowProblem.None;
        }

        #region WowHasCrashed

        private DateTime _crashTimeStamp = DateTime.Now;

        public bool WowHasCrashed
        {
            get
            {
                // check for crash every 10 seconds and cache the result
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    try
                    {
                        if (_wowManager.GameProcess.HasExitedSafe())
                            return true;
                        _crashTimeStamp = DateTime.Now;
                        List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(_wowManager.GameProcess.Id);
                        if (childWinHandles.Select(NativeMethods.GetWindowText).Any(caption => caption == "Wow"))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        if (_wowManager.ProcessIsReadyForInput)
                            return true;
                    }
                }
                return false;
            }
        }

        #endregion

        #region WowIsLoggedOutForTooLong

        private readonly Stopwatch _loggedOutSw = new Stopwatch();
        private DateTime _loggedoutTimeStamp = DateTime.Now;
        private bool _wowIsLoggedOutForTooLong;

        public bool WowIsLoggedOutForTooLong
        {
            get
            {
                // check for crash every 10 seconds and cache the result
                if (DateTime.Now - _loggedoutTimeStamp >= TimeSpan.FromSeconds(5))
                {
                    if (!_wowManager.InGame)
                    {
                        if (!_loggedOutSw.IsRunning)
                            _loggedOutSw.Start();
                        _wowIsLoggedOutForTooLong = _loggedOutSw.ElapsedMilliseconds >= 120000;
                        // reset the timer so it doesn't trigger until 120 more seconds has elapsed while not in game.
                        if (_wowIsLoggedOutForTooLong)
                            _loggedOutSw.Reset();
                    }
                    else if (_loggedOutSw.IsRunning)
                        _loggedOutSw.Reset();
                    _loggedoutTimeStamp = DateTime.Now;
                }
                return _wowIsLoggedOutForTooLong;
            }
        }

        #endregion

        #region WoWIsUnresponsive

        private readonly Stopwatch _wowRespondingSw = new Stopwatch();

        public bool WowIsUnresponsive
        {
            get
            {
                try
                {
                    bool isResponding = _wowManager.GameProcess.Responding;
                    if (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe() && !_wowManager.GameProcess.Responding)
                    {
                        if (!_wowRespondingSw.IsRunning)
                            _wowRespondingSw.Start();
                        if (_wowRespondingSw.ElapsedMilliseconds >= 20000)
                            return true;
                    }
                    else if (isResponding && _wowRespondingSw.IsRunning)
                        _wowRespondingSw.Reset();
                }
                catch (InvalidOperationException)
                {
                    if (_wowManager.ProcessIsReadyForInput)
                        return true;
                }
                return false;
            }
        }

        #endregion

        private enum WowProblem
        {
            None,
            Disconnected,
            LoggedOutForTooLong,
            Unresponsive,
            Crash
        }
    }
}