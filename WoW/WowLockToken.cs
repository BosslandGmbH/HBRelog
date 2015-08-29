using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreyMagic;
using HighVoltz.HBRelog.WoW.FrameXml;
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

        public Task<int> ReuseWowProcessAsync(WowLuaManager luaManager, string characterName)
	    {
	        var tcs1 = new TaskCompletionSource<int>();

            ThreadPool.QueueUserWorkItem(obj =>
            {
                var tcs = obj as TaskCompletionSource<int>;
                if (tcs == null)
                {
                    return;
                }
                var attachedWoW32Pids = HbRelogManager.Settings.CharacterProfiles.Where(
                    p => p.TaskManager.WowManager.GameProcess != null).Select(
                    p => p.TaskManager.WowManager.GameProcess.Id).ToList();

                var wow32Processes = Process.GetProcessesByName("Wow").Where(
                    p => !Utility.Is64BitProcess(p)
                        && p.Responding
                        && !attachedWoW32Pids.Contains(p.Id)).ToList();

                bool doOnce = true;

                foreach (var p in wow32Processes)
                {
                    try
                    {
                        luaManager.Globals = null;
                        luaManager.Memory = new ExternalProcessReader(p);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (luaManager.Memory == null)
                        continue;

                    if (doOnce)
                    {
                        HbRelogManager.Settings.GameStateOffset = (uint)WowPatterns.GameStatePattern.Find(luaManager.Memory);
                        HbRelogManager.Settings.LuaStateOffset = (uint)WowPatterns.LuaStatePattern.Find(luaManager.Memory);
                        HbRelogManager.Settings.FocusedWidgetOffset = (uint)WowPatterns.FocusedWidgetPattern.Find(luaManager.Memory);
                        HbRelogManager.Settings.LoadingScreenEnableCountOffset = (uint)WowPatterns.LoadingScreenEnableCountPattern.Find(luaManager.Memory);
                        HbRelogManager.Settings.GlueStateOffset = (uint)WowPatterns.GlueStatePattern.Find(luaManager.Memory);
                        doOnce = false;
                    }

                    var playerName = "";
                    try
                    {
                        var qwe = from str in UIObject.GetUIObjectsOfType<FontString>(luaManager)
                            where str.IsShown && str.Name == "PlayerName"
                            select str.Text;
                        playerName = qwe.FirstOrDefault();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    if (luaManager.Memory != null)
                        luaManager.Memory.Dispose();
                    luaManager.Globals = null;
                    luaManager.Memory = null;
                    if (!string.IsNullOrEmpty(playerName) && playerName == characterName)
                    {
                        tcs.SetResult(p.Id);
                        return;
                    }
                }
                if (wow32Processes.Any())
                {
                    tcs.SetResult(wow32Processes.First().Id);
                    return;
                }
                tcs.SetResult(-1);
            }, tcs1);

	        return tcs1.Task;
	    }

        public static Task<int> WaitProcessInitAsync(string fileName, string args)
        {
            var tcs = new TaskCompletionSource<int>();
            var process = new Process() { StartInfo = {Arguments = args, FileName = fileName }};
            if (!process.Start())
            {
                //you may allow for the process to be re-used (started = false) 
                //but I'm not sure about the guarantees of the Exited event in such a case
                throw new InvalidOperationException("Could not start process: " + process);
            }
	        ThreadPool.QueueUserWorkItem(_ =>
	        {
                var ok = false;
	            while (!ok)
	            {
                    try
                    {
                        ok = process.MainWindowHandle != IntPtr.Zero;
                    }
                    catch (Exception)
                    {
                    }
                    if (ok)
                    {
                        tcs.SetResult(process.Id);
                    }
	            }
            });
            return tcs.Task;
        }

        public Task<int> ReuseWowProcessTask;
        public Task<int> WowProcessStarterTask;
        /// <summary>
		/// Starts Wow, assigns GameProcess and Memory after lauch and releases lock. Can only call from a valid token
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Lock token is not valid</exception>
        public void StartWoW()
		{
            // TODO: check if HBRelog has enough rights to start/inspect other processes
			lock (LockObject)
			{
				if (!IsValid)
					throw new InvalidOperationException("Lock token is not valid");

                // get valid Wow process which is logged in and PlayerName the same
                // as the CharacterName of current profile
                if (_lockOwner.Settings.ReuseFreeWowProcess
                    && ReuseWowProcessTask == null
                    && WowProcessStarterTask == null
                    && _wowProcess == null)
                {
                    ReuseWowProcessTask = ReuseWowProcessAsync(_lockOwner.LuaManager, _lockOwner.Settings.CharacterName);
                }

			    if (ReuseWowProcessTask != null
                    && ReuseWowProcessTask.IsCompleted)
			    {
                    if (_wowProcess == null && ReuseWowProcessTask.Result > 0)
			        {
			            try
			            {
                            _wowProcess = Process.GetProcessById(ReuseWowProcessTask.Result);
                        }
			            catch (Exception e)
			            {
                            Trace.WriteLine(e);
			            }
			            _lockOwner.ReusedGameProcess = _wowProcess;
			        }
			    }
			    else
			    {
                    // dont fall through is ReuseWowProcessTask is not finished
			        return;
			    }

                if (_wowProcess == null
                    && WowProcessStarterTask != null
                    && WowProcessStarterTask.IsCompleted)
                {
                    try
                    {
                        _wowProcess = Process.GetProcessById(WowProcessStarterTask.Result);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(e);
                    }
                    //if (WowProcessStarterTask.Result == null)
                    //{
                    //    WowProcessStarterTask.Dispose();
                    //    WowProcessStarterTask = null;
                    //}
                }

                if (_wowProcess != null && Utility.Is64BitProcess(_wowProcess))
				{
					_lockOwner.Profile.Log("64 bit Wow is not supported. Delete or rename the WoW-64.exe file in your WoW install folder");
					_lockOwner.Stop();
				}
                
                if (WowProcessStarterTask == null
                    && _wowProcess == null)
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

                    if (_lockOwner.LuaManager.Memory != null)
                        _lockOwner.LuaManager.Memory.Dispose();
				    _lockOwner.LuaManager.Globals = null;
                    _lockOwner.LuaManager.Memory = null;

					// force 32 bit client to start.
                    // lanchingWoW && 
					if (_lockOwner.Settings.WowArgs.IndexOf("-noautolaunch64bit", StringComparison.InvariantCultureIgnoreCase) == -1)
					{
						// append a space to WoW arguments to separate multiple arguments if user is already pasing arguments ..
						if (!string.IsNullOrEmpty(_lockOwner.Settings.WowArgs))
							_lockOwner.Settings.WowArgs += " ";
						_lockOwner.Settings.WowArgs += "-noautolaunch64bit";
					}

                    WowProcessStarterTask = WaitProcessInitAsync(_lockOwner.Settings.WowPath, _lockOwner.Settings.WowArgs);
				}

                if (_wowProcess != null)
				{
					// return if wow isn't ready for input.
					if (_wowProcess.MainWindowHandle == IntPtr.Zero)
					{
						_wowProcess.Refresh();
						_lockOwner.Profile.Status = "Waiting for Wow to start";
						_lockOwner.Profile.Log(_lockOwner.Profile.Status);
						return;
					}
				    _lockOwner.LuaManager.Globals = null;
                    _lockOwner.LuaManager.Memory = new ExternalProcessReader(_wowProcess);
                    _lockOwner.GameProcess = _wowProcess;
				    if (WowProcessStarterTask != null)
				    {
				        WowProcessStarterTask.Dispose();
				        WowProcessStarterTask = null;
				    }
				    if (ReuseWowProcessTask != null)
				    {
				        ReuseWowProcessTask.Dispose();
				        ReuseWowProcessTask = null;
				    }
                    _wowProcess = null;
				    _lockOwner.Profile.Log(_lockOwner.Settings.ReuseFreeWowProcess ? "Wow is ready." : "Wow is ready to login.");
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
