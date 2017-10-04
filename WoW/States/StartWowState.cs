using HighVoltz.HBRelog.FiniteStateMachine;
using System.Diagnostics;
using System.Linq;

namespace HighVoltz.HBRelog.WoW.States
{
	internal class StartWowState : State
	{
		private readonly WowManager _wowManager;

		public StartWowState(WowManager wowManager)
		{
			_wowManager = wowManager;
		}

		public override int Priority
		{
			get { return 1000; }
		}

        public override bool NeedToRun
        {
            get
            {
                var hbManager = _wowManager.Profile.TaskManager.HonorbuddyManager;

                // check if WoW has exited since it was started.
                if (_wowManager.StartupSequenceIsComplete && _wowManager.GameProcessId > 0 && !ProcessExists(_wowManager.GameProcessId))
                    return true;

                return (_wowManager.GameProcess == null || _wowManager.GameProcess.HasExitedSafe()) &&
                        !_wowManager.StartupSequenceIsComplete &&
                        !hbManager.WaitForBotToExit && (hbManager.BotProcess == null || hbManager.BotProcess.HasExitedSafe());
            }
        }

        public override void Run()
		{
			string reason = string.Empty;
			if (_wowManager.LockToken == null || !_wowManager.LockToken.IsValid)
				_wowManager.LockToken = WowLockToken.RequestLock(_wowManager, out reason);

			if (_wowManager.LockToken == null)
			{
				_wowManager.Profile.Status = reason;
				return;
			}
			if (_wowManager.ServerIsOnline)
			{
				_wowManager.LockToken.StartWoW();
			}
			else
			{
				_wowManager.Profile.Status = string.Format("{0} is offline", _wowManager.Settings.ServerName);
				_wowManager.Profile.Log("Server is offline");
			}
		}

        private bool ProcessExists(int id)
        {
            bool result = false;
            foreach (Process p in Process.GetProcesses())
            {
                result |= p.Id == id;
                p.Dispose();
            }
            return result;
        }

    }
}