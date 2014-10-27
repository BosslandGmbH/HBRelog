using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GreyMagic;
using HighVoltz.Launcher;

namespace HighVoltz.HBRelog.WoW
{
	public class WowLockToken : IDisposable
	{
		private readonly DateTime _startTime;
		private WowManager _lockOwner;
		private readonly string _key;
		private Process _wowProcess;
		private int _launcherPid;

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
				_launcherPid = 0;
				_lockOwner = null;
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

				if (_launcherPid > 0)
				{
                    Process wowProcess = Utility.GetChildProcessByName(_launcherPid, "Wow") 
                        ?? Utility.GetChildProcessByName(_launcherPid, "WowB")  // Beta
                        ?? Utility.GetChildProcessByName(_launcherPid, "WowT"); // PTR
					if (wowProcess != null)
					{
						_launcherPid = 0;
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
                    if (_wowProcess != null && _wowProcess.HasExitedSafe() && DateTime.Now - _startTime < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay))
					{
						return;
					}
					AdjustWoWConfig();
					_lockOwner.Profile.Log("Starting {0}", _lockOwner.Settings.WowPath);
					_lockOwner.Profile.Status = "Starting WoW";

					_lockOwner.StartupSequenceIsComplete = false;
					_lockOwner.Memory = null;

					bool lanchingWoW = _lockOwner.Settings.WowPath.IndexOf("WoW.exe", StringComparison.InvariantCultureIgnoreCase) != -1
                         || _lockOwner.Settings.WowPath.IndexOf("WoWB.exe", StringComparison.InvariantCultureIgnoreCase) != -1 // Beta WoW
                         || _lockOwner.Settings.WowPath.IndexOf("WoWT.exe", StringComparison.InvariantCultureIgnoreCase) != -1;// PTR WoW

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

					_launcherPid = Process.Start(pi).Id;
					_lockOwner.ProcessIsReadyForInput = false;
					_lockOwner.LoginTimer.Reset();
				}
				else
				{
					// return if wow isn't ready for input.
					if (_wowProcess.MainWindowHandle == IntPtr.Zero)
					{
						_wowProcess.Refresh();
						_lockOwner.Profile.Status = "Waiting for Wow to start";
						_lockOwner.Profile.Log(_lockOwner.Profile.Status);
						return;
					}
					_lockOwner.GameProcess = _wowProcess;
					_lockOwner.Memory = new ExternalProcessReader(_wowProcess);
					_wowProcess = null;
					_lockOwner.Profile.Log("Wow is ready to login.");
				}
			}
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
			config.EnsureValue("realmName", _lockOwner.Settings.ServerName);
			if (HbRelogManager.Settings.AutoAcceptTosEula)
			{
				config.EnsureValue("readTOS", "1");
				config.EnsureValue("readEULA", "1");
			}
			config.EnsureValue("accountName", _lockOwner.Settings.Login);

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
