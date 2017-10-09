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
            get { return 700; }
        }


        public override bool NeedToRun
        {
            get
            {
                if (_wowManager.GameWindow == IntPtr.Zero)
                    return false;

                _isCrashed = CheckIsCrashed();
                if (_isCrashed)
                    return true;

                _isUnresponsive = CheckIsUnresponsive();
                if (_isUnresponsive)
                    return true;

                return (_wowManager.IsConnectingOrLoading || _wowManager.InGame)
                    && (_wowManager.LockToken.IsValid || _wowManager.Memory != null || !_wowManager.StartupSequenceIsComplete);
            }
        }

        public override void Run()
        {
            if (_isCrashed || _isUnresponsive)
                CrashHangLogic();
            else
                InGameLogic();
        }

        private void InGameLogic()
        {
            if (_wowManager.LockToken.IsValid)
                _wowManager.LockToken.ReleaseLock();

            // The HBRelogHelper plugin will monitor the health of the WoW process
            if (_wowManager.Memory != null)
            {
                _wowManager.Memory.Dispose();
                _wowManager.Memory = null;
            }

            if (!_wowManager.StartupSequenceIsComplete)
            {
                _wowManager.SetStartupSequenceToComplete();
            }
        }

        private void CrashHangLogic()
        {
            var verb = _isCrashed ? "crashed" : "hung";
            _wowManager.Profile.Status = $"WoW has {verb}. Restarting";
            _wowManager.Profile.Log($"WoW has {verb}, so lets restart WoW");
            _wowManager.CloseGameProcess();
        }

        private readonly Stopwatch _unresponsivenessCheckTimer = new Stopwatch();
        private int _unresponsiveCount;
        private bool _isUnresponsive;

        public bool CheckIsUnresponsive()
        {
            if (!HbRelogManager.Settings.CheckWowResponsiveness)
                return false;

            if (_unresponsivenessCheckTimer.IsRunning && _unresponsivenessCheckTimer.ElapsedMilliseconds < 10000)
                return false;

            bool isResponding = _wowManager.WaitForMessageHandler(15000);
            if (isResponding)
                _unresponsiveCount = 0;
            else
                _unresponsiveCount++;

            _unresponsivenessCheckTimer.Restart();
            return _unresponsiveCount >= 3;
        }

        private readonly Stopwatch _crashCheckTimer = new Stopwatch();
        private bool _isCrashed;
        private bool CheckIsCrashed()
        {
            if (_crashCheckTimer.IsRunning && _crashCheckTimer.ElapsedMilliseconds < 10000)
                return false;

            List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(_wowManager.GameProcessId);
            string procName = _wowManager.GameProcessName;

            if (childWinHandles.Select(NativeMethods.GetWindowText).Any(caption => string.Equals(caption, procName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            _crashCheckTimer.Restart();
            return false;
        }
    }
}