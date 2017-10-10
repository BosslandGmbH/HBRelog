using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GreyMagic;
using HighVoltz.Launcher;
using System.Runtime.InteropServices;
using HighVoltz.HBRelog.Settings;

namespace HighVoltz.HBRelog.WoW
{
	public class WowLockToken : IDisposable
	{
		private readonly DateTime _startTime;
		private WowManager _lockOwner;
		private readonly string _key;
		private Process _wowProcess;
		private Process _launcherProc;
        private Stopwatch _dialogDisplayTimer;

		private WowLockToken(string key, DateTime startTime, WowManager lockOwner)
		{
			_startTime = startTime;
			_lockOwner = lockOwner;
			_key = key;
		}

		public void ReleaseLock()
		{
			Dispose();
		}

		public bool IsValid
		{
			get
			{
				lock (LockObject)
				{
					return _lockOwner != null && LockInfos.ContainsKey(_key) && LockInfos[_key]._lockOwner == _lockOwner;
				}
			}
		}

		public void Dispose()
		{
			lock (LockObject)
			{
                if (_wowProcess != null && !_wowProcess.HasExitedSafe())
				{
					_wowProcess.Kill();
				}
				_wowProcess = null;
                if (_launcherProc != null)
                {
                    _launcherProc.Dispose();
                    _launcherProc = null;
                }
                _lockOwner = null;
                _dialogDisplayTimer = null;
            }
		}


		/// <summary>
		/// Starts Wow, assigns GameProcess and Memory after lauch and releases lock. Can only call from a valid token
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Lock token is not valid</exception>
		public void StartWoW()
		{
			lock (LockObject)
			{
				if (!IsValid)
					throw new InvalidOperationException("Lock token is not valid");

				if (_wowProcess != null && Utility.Is64BitProcess(_wowProcess))
				{
					_lockOwner.Profile.Log("64 bit Wow is not supported. Delete or rename the WoW-64.exe file in your WoW install folder");
					_lockOwner.Stop();
				}

				// check if a batch file or any .exe besides WoW.exe is used and try to get the child WoW process started by this process.

				if (_launcherProc != null)
				{
					Process wowProcess;
					// Two methods are used to find the WoW process if a launcher is used;
					// Method one: launcher exit code return the WoW process ID or a negative if an error occured. 
					//			   If launcher does not use return the expected return values then a batch file or console app
					//			   must be used to start the launcher and return the expected return codes.	
					// Method two: Find a child WoW process of the launcher process.

					if (_launcherProc.HasExited && _launcherProc.ExitCode < 0)
					{
						_lockOwner.Profile.Log("Pausing profile because launcher exited with error code: {0}", _launcherProc.ExitCode);
						_lockOwner.Profile.Pause();
						return;
					}

					if (!_launcherProc.HasExited || _launcherProc.ExitCode  == 0 
                        || !Utility.TryGetProcessById(_launcherProc.ExitCode, out wowProcess))
					{
						var executablePath = Path.GetFileNameWithoutExtension(_lockOwner.Profile.Settings.WowSettings.WowPath);
						wowProcess = Utility.GetChildProcessByName(_launcherProc.Id, "Wow") 
							?? Utility.GetChildProcessByName(_launcherProc.Id, "WowB")  // Beta
							?? Utility.GetChildProcessByName(_launcherProc.Id, "WowT")	// PTR
							?? Utility.GetChildProcessByName(_launcherProc.Id, executablePath); // Renamed executables
					}
					if (wowProcess != null)
					{
                        _lockOwner.Profile.Log($"WoW Launcher Pid: {_launcherProc.Id} launched WoW Pid: {wowProcess.Id}");
                        _launcherProc.Dispose();
                        _launcherProc = null;
                        _dialogDisplayTimer = null;
                        Helpers.ResumeProcess(wowProcess.Id);
						_wowProcess = wowProcess;
					}
					else
					{
						_lockOwner.Profile.Log("Waiting on external application to start WoW");
						_lockOwner.Profile.Status = "Waiting on external application to start WoW";
						return;
					}

				}

                if (_wowProcess == null || _wowProcess.HasExitedSafe())
				{
                    // throttle the number of times wow is launched.
                    if (_wowProcess != null && _wowProcess.HasExitedSafe()
                        && DateTime.Now - _startTime < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay))
                        return;

                    StartWowProcess();
                    return;
				}
                // need to refresh everytime because of the dialog at startup
                _wowProcess.Refresh();
                // return if wow isn't ready for input.
                IntPtr wowWindow = FindWowWindowHandle(_wowProcess);
                if (wowWindow == IntPtr.Zero)
				{
					_lockOwner.Profile.Status = "Waiting for Wow to start";
					_lockOwner.Profile.Log(_lockOwner.Profile.Status);
					return;
				}
				var isPopup = (NativeMethods.GetWindowStyle(_wowProcess.MainWindowHandle) & NativeMethods.WindowStyle.Popup) != 0;

                if (isPopup)
				{
                    // WoW is now shown as a dialog very breifly everytime it's started, 
                    // so we only care if it's a dialog window longer than normal
                    if (_dialogDisplayTimer == null)
                    {
                        _dialogDisplayTimer = Stopwatch.StartNew();
                    }
                    else if (_dialogDisplayTimer.ElapsedMilliseconds > 30000)
                    {
                        _lockOwner.Profile.Log($"WoW v{_wowProcess.VersionString()} failed to load and is a popup. " +
                            $"Make sure your WoW installation is updated. Pausing profile.");
                        _lockOwner.Profile.Pause();
                        ReleaseLock();
                    }
					return;
				}
                _lockOwner.GameWindow = wowWindow;
                _lockOwner.GameProcessId = _wowProcess.Id;
                _lockOwner.GameProcessName = _wowProcess.ProcessName;
                _lockOwner.Memory = new ExternalProcessReader(_wowProcess);
                _wowProcess = null;
				_lockOwner.Profile.Log("Wow is ready to login.");
			}
		}

        private void StartWowProcess()
        {
            AdjustWoWConfig();
            _lockOwner.Profile.Log("Starting {0}", _lockOwner.Settings.WowPath);
            _lockOwner.Profile.Status = "Starting WoW";

            _lockOwner.StartupSequenceIsComplete = false;
            if (_lockOwner.Memory != null)
            {
                _lockOwner.Memory.Dispose();
                _lockOwner.Memory = null;
            }
            _dialogDisplayTimer = null;
            bool lanchingWoW = IsWoWPath(_lockOwner.Settings.WowPath);

            // force 32 bit client to start.
            if (lanchingWoW && _lockOwner.Settings.WowArgs.IndexOf("-noautolaunch64bit", StringComparison.InvariantCultureIgnoreCase) == -1)
            {
                // append a space to WoW arguments to separate multiple arguments if user is already pasing arguments ..
                if (!string.IsNullOrEmpty(_lockOwner.Settings.WowArgs))
                    _lockOwner.Settings.WowArgs += " ";
                _lockOwner.Settings.WowArgs += "-noautolaunch64bit";
            }

            var pi = new ProcessStartInfo() { UseShellExecute = false };

            if (lanchingWoW)
            {
                var launcherPath = Path.Combine(Utility.AssemblyDirectory, "Launcher.exe");
                pi.FileName = launcherPath;
                var args = string.Format("\"{0}\" \"{1}\"", _lockOwner.Settings.WowPath, _lockOwner.Settings.WowArgs);
                pi.Arguments = args;
            }
            else
            {
                pi.FileName = _lockOwner.Settings.WowPath;
                pi.Arguments = _lockOwner.Settings.WowArgs;
            }

            _launcherProc = Process.Start(pi);
            _lockOwner.ProcessIsReadyForInput = false;
            _lockOwner.LoginTimer.Reset();
        }


        private IntPtr FindWowWindowHandle(Process wowProcess)
        {
            return
                NativeMethods.EnumerateProcessWindowHandles(wowProcess.Id)
                                 .FirstOrDefault(h => GetClassName(h) == "GxWindowClass");
        }

        private string GetClassName(IntPtr window)
        {
            // Pre-allocate 256 characters, since this is the maximum class name length.
            StringBuilder className = new StringBuilder(256);
            //Get the window class name
            var nRet = GetClassName(window, className, className.Capacity);
            if (nRet != 0)
                return className.ToString();

            return "";
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);


        private bool IsWoWPath(string path)
		{
			var originalExeFileName = FileVersionInfo.GetVersionInfo(path).OriginalFilename;
			return originalExeFileName == "WoW.exe"
				|| originalExeFileName == "WoWB.exe" // Beta WoW
				|| originalExeFileName == "WoWT.exe" ;// PTR WoW
		}

		bool IsWoWProcess(Process proc)
		{
			return proc.ProcessName.Equals("Wow", StringComparison.CurrentCultureIgnoreCase);
		}

		bool IsCmdProcess(Process proc)
		{
			return proc.ProcessName.Equals("cmd", StringComparison.CurrentCultureIgnoreCase);
		}


		private void AdjustWoWConfig()
		{
			var wowFolder = Path.GetDirectoryName(_lockOwner.Settings.WowPath);
			var configPath = Path.Combine(wowFolder, @"Wtf\Config.wtf");
			if (!File.Exists(configPath))
			{
				_lockOwner.Profile.Log("Warning: Unable to find Wow's config.wtf file. Editing this file speeds up the login process.");
				return;
			}
			var config = new ConfigWtf(_lockOwner, configPath);
			if (HbRelogManager.Settings.AutoAcceptTosEula)
			{
				config.EnsureValue("readTOS", "1");
				config.EnsureValue("readEULA", "1");
			}
			config.EnsureValue("accountName", _lockOwner.Settings.Login);

            if (_lockOwner.Settings.Region != WowSettings.WowRegion.Auto)
            {
                string portal;
                switch (_lockOwner.Settings.Region)
                {
                    case WowSettings.WowRegion.US:
                        portal = "US";
                        break;
                    case WowSettings.WowRegion.EU:
                        portal = "EU";
                        break;
                    case WowSettings.WowRegion.Korea:
                        portal = "KR";
                        break;
                    case WowSettings.WowRegion.China:
                        portal = "CN";
                        break;
                    case WowSettings.WowRegion.Taiwan:
                        portal = "TW";
                        break;
                    default:
                        portal = null;
                        break;
                }

                if (portal != null)
                    config.EnsureValue("portal", portal);
            }

            if (!string.IsNullOrEmpty(_lockOwner.Settings.AcountName))
				config.EnsureAccountList(_lockOwner.Settings.AcountName);
			else
				config.DeleteSetting("accountList");

			if (config.Changed)
			{
				config.Save();
			}
		}

		#region Static members.

		private static readonly object LockObject = new object();
		private static readonly Dictionary<string, WowLockToken> LockInfos = new Dictionary<string, WowLockToken>();
		private static DateTime _lastLockTime;
		public static WowLockToken RequestLock(WowManager wowManager, out string reason)
		{
			reason = string.Empty;
			WowLockToken ret;
			string key = wowManager.Settings.WowPath.ToUpper();
			lock (LockObject)
			{
				var now = DateTime.Now;
				bool throttled = now - _lastLockTime < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay);
				if (throttled)
				{
					reason = "Waiting to start WoW";
					return null;
				}

				if (LockInfos.ContainsKey(key))
				{
					var lockInfo = LockInfos[key];
					var locked = lockInfo._lockOwner != null && lockInfo._lockOwner != wowManager;
					if (locked)
					{
						reason = string.Format("Waiting on profile: {0} to release lock", lockInfo._lockOwner.Profile.Settings.ProfileName);
						return null;
					}
				}
				_lastLockTime = now;
				ret = LockInfos[key] = new WowLockToken(key, DateTime.Now, wowManager);
			}
			return ret;
		}

		#endregion

	}
}
