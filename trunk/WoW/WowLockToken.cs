using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GreyMagic;

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
                if (_wowProcess != null && !_wowProcess.HasExited)
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

                if (_wowProcess == null || _wowProcess.HasExited)
                {
                    AdjustWoWConfig();
                    _lockOwner.Profile.Log("Starting {0}", _lockOwner.Settings.WowPath);
                    _lockOwner.Profile.Status = "Starting WoW";

                    _lockOwner.StartupSequenceIsComplete = false;
                    _lockOwner.Memory = null;

                    var pi = new ProcessStartInfo(_lockOwner.Settings.WowPath, _lockOwner.Settings.WowArgs);

                    _wowProcess = Process.Start(pi);

                    _lockOwner.ProcessIsReadyForInput = false;
                    _lockOwner.LoginTimer.Reset();

                    if (_lockOwner.IsUsingLauncher)
                    {
                        _launcherPid = _wowProcess.Id;
                        // set GameProcess temporarily to HBRelog because laucher can exit before wow starts 
                        _wowProcess = Process.GetCurrentProcess();
                    }
                }
                else
                {
                    // check if a batch file or any .exe besides WoW.exe is used and try to get the child WoW process started by this process.
                    if (_lockOwner.IsUsingLauncher && !Path.GetFileName(_wowProcess.MainModule.FileName).Equals("Wow.exe", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Process wowProcess = null;
                        if (_launcherPid > 0)
                        {
                            wowProcess = Utility.GetChildProcessByName(_launcherPid, "Wow");
                        }
                        else
                        {
                            // seems like the launcher process terminated early before we could grab the Game process that it started.. 
                            // so we just find the 1st game process that's not monitor by HBRelog.
                            var processes = Process.GetProcessesByName("Wow");
                            foreach (var characterProfile in HbRelogManager.Settings.CharacterProfiles.Where(c => c.IsRunning && !c.IsPaused))
                            {
                                var proc = characterProfile.TaskManager.WowManager.GameProcess;
                                if (proc == null || proc.HasExited || processes.Any(p => p.Id == proc.Id))
                                    continue;
                                wowProcess = proc;
                                break;
                            }
                        }

                        if (wowProcess == null)
                        {
                            _lockOwner.Profile.Log("Waiting on external application to start WoW");
                            _lockOwner.Profile.Status = "Waiting on external application to start WoW";
                            return;
                        }
                        _wowProcess = wowProcess;
                    }
                    // return if wow isn't ready for input.
                    if (!_wowProcess.WaitForInputIdle(0))
                    {
                        _lockOwner.Profile.Status = "Waiting for Wow to start";
                        _lockOwner.Profile.Log(_lockOwner.Profile.Status);
                        return;
                    }
                    _lockOwner.GameProcess = _wowProcess;
                    _lockOwner.Memory = new ExternalProcessReader(_wowProcess);
                    _wowProcess = null;
                    _lockOwner.Profile.Log("Wow is ready to login.");
                    ReleaseLock();
                }
            }
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
            if (config.Changed)
            {
                config.Save();
            }
        }

        #region Static members.

        private static readonly object LockObject = new object();
        private static readonly Dictionary<string, WowLockToken> LockInfos = new Dictionary<string, WowLockToken>();

        public static WowLockToken RequestLock(WowManager wowManager, out string reason)
        {
            reason = string.Empty;
            WowLockToken ret;
            string key = wowManager.Settings.WowPath.ToUpper();
            lock (LockObject)
            {
                if (LockInfos.ContainsKey(key))
                {
                    var lockInfo = LockInfos[key];
                    bool throttled = DateTime.Now - lockInfo._startTime < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay);
                    bool locked = lockInfo._lockOwner != null && lockInfo._lockOwner != wowManager;
                    if (throttled || locked)
                    {
                        reason = throttled
                                     ? "Waiting to start WoW"
                                     : string.Format("Waiting on profile: {0} to release lock", lockInfo._lockOwner.Profile.Settings.ProfileName);
                        return null;
                    }
                }
                ret = LockInfos[key] = new WowLockToken(key, DateTime.Now, wowManager);
            }
            return ret;
        }

        #endregion

    }
}
