using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using GreyMagic;
using HighVoltz.HBRelog.Settings;

namespace HighVoltz.HBRelog.WoW
{
    public sealed class WowManager : IGameManager
    {
        private const string LoginLuaFormat =
        @"local acct = ""{2}""
        if (WoWAccountSelectDialog and WoWAccountSelectDialog:IsShown()) then    
            for i = 1, GetNumGameAccounts() do    
                if GetGameAccountInfo(i):upper() == acct:upper() then    
                    WoWAccountSelect_SelectAccount(i)    
                end    
            end    
        elseif (AccountLoginUI and AccountLoginUI:IsVisible()) then    
            if (AccountLoginDropDown:IsShown()) then    
                for i=1, #AccountList  do    
                    if AccountList[i].text:upper() == acct:upper() then    
                        GlueDropDownMenu_SetSelectedName(AccountLoginDropDown,AccountList[i].text)    
                        GlueDialog_Show('ACCOUNT_MSG',AccountList[i].text)    
                    end    
                end     
            end    
            DefaultServerLogin(""{0}"" ,""{1}"" )    
            AccountLoginUI:Hide()    
        end";

        // indexes are {0}=character, {1}=server
        private const string CharSelectLuaFormat =
             @"local name = ""{0}""
             local server = ""{1}""     
             if (CharacterSelectUI and CharacterSelectUI:IsVisible()) then    
                 if GetServerName():upper() ~= server:upper() and (not RealmList or not RealmList:IsVisible()) then    
                    RequestRealmList(1)    
                 else    
                     if (GetCharacterInfo(CharacterSelect.selectedIndex):upper() == name:upper()) then    
                        CharSelectEnterWorldButton:Click()    
                     else    
                         for i = 1,GetNumCharacters() do    
                             if (GetCharacterInfo(i):upper() == name:upper()) then    
                                 CharacterSelect_SelectCharacter(i)    
                                 return    
                             end    
                         end    
                     end   
 

                --  for i = 1,GetNumCharacters() do    
                --  local name = GetCharacterInfo(i)    
                --  GlueDialog_Show('ACCOUNT_MSG',name:upper()..':'..CharacterSelect.selectedIndex)    
                -- if (GetCharacterInfo(i):upper() == name:upper()) then    
                --                 CharacterSelect_SelectCharacter(i)    
                --                 CharSelectEnterWorldButton:Click()    
                --  end    
                --  end    


                 end      
             end ";

        // indexes are {0}=server
        private const string RealmSelectLuaFormat =
             @"local server = ""{0}""
             if (RealmList and RealmList:IsVisible()) then    
                 for i = 1, select('#',GetRealmCategories()) do    
                     for j = 1, GetNumRealms(i) do    
                         if GetRealmInfo(i, j):upper() == server:upper() then    
                             RealmList:Hide()    
                             ChangeRealm(i, j)    
                         end    
                     end    
                 end    
             end";

        private readonly object _lockObject = new object();
        private readonly Stopwatch _loggedOutSw = new Stopwatch();
        private readonly Stopwatch _serverSelectionSw = new Stopwatch();
        private readonly Stopwatch _wowRespondingSw = new Stopwatch();
        private string _charSelectLua;
        private DateTime _crashTimeStamp = DateTime.Now;
        private bool _isExiting;
        private GlueState _lastGlueStatus = GlueState.None;
        private DateTime _loggedoutTimeStamp = DateTime.Now;
        private string _loginLua;
        private DateTime _luaThrottleTimeStamp = DateTime.Now;
        private bool _processIsReadyForInput;
        private CharacterProfile _profile;
        private string _realmSelectLua;
        private bool _waitingToStart;
        private int _windowCloseAttempt;
        private Timer _wowCloseTimer;
        private bool _wowIsLoggedOutForTooLong;
        private Timer _wowLoginTimer;

        public WowManager(CharacterProfile profile)
        {
            Profile = profile;
        }

        public WowSettings Settings { get; private set; }

        public ExternalProcessReader Memory
        {
            get { return WowHook != null ? WowHook.Memory : null; }
        }

        public Lua Lua { get; set; }
        public Hook WowHook { get; private set; }

        /// <summary>
        /// WoW is at the connecting or loading screen
        /// </summary>
        public bool IsConnectiongOrLoading
        {
            get
            {
                try
                {
                    return WowHook != null &&
                           Memory.Read<byte>(true, ((IntPtr)HbRelogManager.Settings.GameStateOffset + 1)) ==
                           1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public GlueState GlueStatus
        {
            get
            {
                return WowHook != null
                           ? (GlueState)
                             Memory.Read<GlueState>(true, (IntPtr)HbRelogManager.Settings.GlueStateOffset)
                           : GlueState.Disconnected;
            }
        }

        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        public bool WoWIsResponding
        {
            get
            {
                try
                {
                    bool isResponding = GameProcess.Responding;
                    if (GameProcess != null && !GameProcess.HasExited && !GameProcess.Responding)
                    {
                        if (!_wowRespondingSw.IsRunning)
                            _wowRespondingSw.Start();
                        if (_wowRespondingSw.ElapsedMilliseconds >= 20000)
                            return false;
                    }
                    else if (isResponding && _wowRespondingSw.IsRunning)
                        _wowRespondingSw.Reset();
                }
                catch (InvalidOperationException)
                {
                    if (_processIsReadyForInput)
                        return false;
                }
                return true;
            }
        }

        public bool WowHasCrashed
        {
            get
            {
                // check for crash every 10 seconds and cache the result
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    try
                    {
                        if (GameProcess.HasExited)
                            return true;
                        _crashTimeStamp = DateTime.Now;
                        List<IntPtr> childWinHandles = NativeMethods.EnumerateProcessWindowHandles(GameProcess.Id);
                        if (childWinHandles.Select(NativeMethods.GetWindowText).Any(caption => caption == "Wow"))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        if (_processIsReadyForInput)
                            return true;
                    }
                }
                return false;
            }
        }

        public bool WowIsLoggedOutForTooLong
        {
            get
            {
                // check for crash every 10 seconds and cache the result
                if (DateTime.Now - _loggedoutTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    if (!InGame)
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

        #region IGameManager Members

        public CharacterProfile Profile
        {
            get { return _profile; }
            private set
            {
                _profile = value;
                Settings = value.Settings.WowSettings;
            }
        }

        public Process GameProcess { get; private set; }

        public void SetSettings(WowSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        /// Character is logged in game
        /// </summary>
        public bool InGame
        {
            get
            {
                try
                {
                    return WowHook != null &&
                           Memory.Read<byte>(true, (IntPtr)HbRelogManager.Settings.GameStateOffset) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool IsRunning { get; private set; }
        public bool StartupSequenceIsComplete { get; private set; }
        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;

        public void Start()
        {
            lock (_lockObject)
            {
                if (File.Exists(Settings.WowPath))
                {
                    IsRunning = true;
                    StartWoW();
                }
                else
                    MessageBox.Show(string.Format("path to WoW.exe does not exist: {0}", Settings.WowPath));
            }
        }

        public void Stop()
        {
            // try to aquire lock, if fail then kill process anyways.
            bool lockAquried = Monitor.TryEnter(_lockObject, 500);
            if (IsRunning)
            {
                if (WowHook != null && WowHook.Installed)
                    WowHook.DisposeHooking();
                if (_wowLoginTimer != null)
                    _wowLoginTimer.Dispose();
                WowHook = null;
                CloseGameProcess();
                IsRunning = false;
                StartupSequenceIsComplete = false;
            }
            if (lockAquried)
                Monitor.Exit(_lockObject);
        }


        public void Pulse()
        {
            lock (_lockObject)
            {
                if (IsRunning)
                {
                    // restart wow WoW if it has exited
                    if (GameProcess == null || GameProcess.HasExited)
                    {
                        if (_waitingToStart)
                            Profile.Status = "Waiting to start";
                        else
                        {
                            Profile.Log("WoW process was terminated. Restarting");
                            Profile.Status = "WoW process was terminated. Restarting";
                        }
                        StartWoW();
                        return;
                    }
                    // return if wow isn't ready for input.
                    if (!GameProcess.WaitForInputIdle(0))
                        return;
                    if (WowHook == null)
                    {
                        WowHook = new Hook(GameProcess);
                    }
                    if (!StartupSequenceIsComplete && !InGame && !IsConnectiongOrLoading)
                    {
                        if (!WowHook.Installed)
                        {
                            Profile.Log("Installing Endscene hook");
                            Profile.Status = "Logging into WoW";
                            // check if we need to scan for offsets
                            if (string.IsNullOrEmpty(HbRelogManager.Settings.WowVersion) ||
                                !HbRelogManager.Settings.WowVersion.Equals(GameProcess.VersionString()))
                            {
                                ScanForOffset();
                            }
                            WowHook.InstallHook();
                            Lua = new Lua(WowHook);
                            UpdateLoginString();
                        }
                        // hook is installed so lets assume proces is ready for input.
                        else if (!_processIsReadyForInput)
                        {
                            // change window title
                            NativeMethods.SetWindowText(GameProcess.MainWindowHandle, string.Format("{0} - ProcID: {1}", Profile.Settings.ProfileName, GameProcess.Id));
                            // resize and position window.
                            if (Settings.WowWindowWidth > 0 && Settings.WowWindowHeight > 0)
                            {
                                Profile.Log("Setting Window location to X:{0}, Y:{1} and Size to Width {2}, Height:{3}",
                                            Settings.WowWindowX, Settings.WowWindowY,
                                            Settings.WowWindowWidth, Settings.WowWindowHeight);

                                Utility.ResizeAndMoveWindow(GameProcess.MainWindowHandle, Settings.WowWindowX,
                                                            Settings.WowWindowY,
                                                            Settings.WowWindowWidth, Settings.WowWindowHeight);
                            }
                            _processIsReadyForInput = true;
                        }
                        LoginWoW();
                    }
                    // remove hook since its nolonger needed.
                    if (WowHook.Installed && (InGame || IsConnectiongOrLoading) && WowHook != null)
                    {
                        Profile.Log("Login sequence complete. Removing hook");
                        Profile.Status = "Logged into WoW";
                        WowHook.DisposeHooking();
                        StartupSequenceIsComplete = true;
                        if (OnStartupSequenceIsComplete != null)
                            OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
                    }
                    // if WoW has disconnected or crashed close wow and start the login sequence again.

                    if ((StartupSequenceIsComplete && (GlueStatus == GlueState.Disconnected || WowIsLoggedOutForTooLong))
                        || !WoWIsResponding || WowHasCrashed)
                    {
                        if (!WoWIsResponding)
                        {
                            Profile.Status = "WoW is not responding. restarting";
                            Profile.Log("WoW is not responding.. So lets restart WoW");
                        }
                        else if (WowHasCrashed)
                        {
                            Profile.Status = "WoW has crashed. restarting";
                            Profile.Log("WoW has crashed.. So lets restart WoW");
                        }
                        else if (WowIsLoggedOutForTooLong)
                        {
                            Profile.Log("Restarting wow because it was logged out for more than 40 seconds");
                            Profile.Status = "WoW was logged out for too long. restarting";
                        }
                        else
                        {
                            Profile.Log("WoW has disconnected.. So lets restart WoW");
                            Profile.Status = "WoW has DCed. restarting";
                        }
                        CloseGameProcess();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Starts the WoW process
        /// </summary>
        private void StartWoW()
        {
            _waitingToStart = !WowStartupManager.CanStart(Settings.WowPath);
            if (_waitingToStart)
                return;
            Profile.Log("starting {0}", Settings.WowPath);
            Profile.Status = "Starting WoW";
            _processIsReadyForInput = StartupSequenceIsComplete = false;
            WowHook = null;
            Lua = null;
            GameProcess = Process.Start(Settings.WowPath);
            _wowLoginTimer = new Timer(WowLoginTimerCallBack, null, 0, 10000);
        }

        private void WowLoginTimerCallBack(object state)
        {
            try
            {
                // once hook to endscene is removed then we can dispose this timer and check for crashes from the main thread.
                if (!IsRunning || StartupSequenceIsComplete)
                    _wowLoginTimer.Dispose();
                else if (!Profile.IsPaused && (!WoWIsResponding || WowHasCrashed))
                {
                    if (!WoWIsResponding)
                    {
                        Profile.Status = "WoW is not responding. restarting";
                        Profile.Log("WoW is not responding.. So lets restart WoW");
                    }
                    else if (WowHasCrashed)
                    {
                        Profile.Status = "WoW has crashed. restarting";
                        Profile.Log("WoW has crashed.. So lets restart WoW");
                    }
                    CloseGameProcess();
                    _wowLoginTimer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        public void CloseGameProcess()
        {
            try
            {
                CloseGameProcess(GameProcess);
            }
            // handle the "No process is associated with this object' exception while wow process is still 'active'
            catch (InvalidOperationException ex)
            {
                Log.Err(ex.ToString());
                if (WowHook != null)
                    CloseGameProcess(Process.GetProcessById(WowHook.ProcessId));
            }
            //Profile.TaskManager.HonorbuddyManager.CloseBotProcess();
            GameProcess = null;
        }

        private void CloseGameProcess(Process proc)
        {
            if (!_isExiting && proc != null && !proc.HasExited)
            {
                _isExiting = true;
                Profile.Log("Attempting to close Wow");
                proc.CloseMainWindow();
                _windowCloseAttempt++;
                _wowCloseTimer = new Timer(state =>
                                               {
                                                   if (!((Process)state).HasExited)
                                                   {
                                                       if (_windowCloseAttempt++ < 6)
                                                           proc.CloseMainWindow();
                                                       else
                                                       {
                                                           Profile.Log("Killing Wow");
                                                           ((Process)state).Kill();
                                                       }
                                                   }
                                                   else
                                                   {
                                                       _isExiting = false;
                                                       Profile.Log("Successfully closed Wow");
                                                       _wowCloseTimer.Dispose();
                                                       _windowCloseAttempt = 0;
                                                   }
                                               }, proc, 1000, 1000);
            }
        }

        private void LoginWoW()
        {
            // throttle lua calls.
            if (DateTime.Now - _luaThrottleTimeStamp >= TimeSpan.FromSeconds(HbRelogManager.Settings.LoginDelay))
            {
                bool serverIsOnline = !HbRelogManager.Settings.CheckRealmStatus ||
                                      (HbRelogManager.Settings.CheckRealmStatus &&
                                       HbRelogManager.WowRealmStatus.RealmIsOnline(Settings.ServerName, Settings.Region));
                // if we are checking for wow server status and the wow server is offline then return
                if (serverIsOnline)
                {
                    GlueState glueStatus = GlueStatus;
                    // check if at server selection for tooo long.
                    if (glueStatus == _lastGlueStatus)
                    {
                        if (!_serverSelectionSw.IsRunning)
                            _serverSelectionSw.Start();
                        WowRealmStatus.WowRealmStatusEntry status =
                            HbRelogManager.WowRealmStatus[Settings.ServerName, Settings.Region];
                        bool serverHasQueue = HbRelogManager.Settings.CheckRealmStatus && status != null &&
                                              status.InQueue;
                        // check once every 40 seconds
                        if (_serverSelectionSw.ElapsedMilliseconds > 40000 && !serverHasQueue)
                        {
                            Profile.Log("Failed to login wow, lets restart");
                            GameProcess.Kill();
                            StartWoW();
                            // set to 'None' to prevent an infinite loop if set to 'Disconnected'
                            _lastGlueStatus = GlueState.None;
                            return;
                        }
                    }
                    else if (_serverSelectionSw.IsRunning)
                        _serverSelectionSw.Reset();

                    AntiAfk();
                    switch (glueStatus)
                    {
                        case GlueState.Disconnected:
                            Profile.Status = "Logging in";
                            Lua.DoString(_loginLua);
                            break;
                        case GlueState.CharacterSelection:
                            Profile.Status = "At Character Selection";
                            Lua.DoString(_charSelectLua);
                            break;
                        case GlueState.ServerSelection:
                            Profile.Status = "At Server Selection";
                            Lua.DoString(_realmSelectLua);
                            break;
                        case GlueState.CharacterCreation:
                            Lua.DoString(
                                "CharacterCreate_Back()");
                            break;
                        case GlueState.Updater:
                            Profile.Pause();
                            Profile.Log("Wow is updating. pausing.");
                            break;
                    }
                    Profile.Log("GlueStatus: {0}", GlueStatus);
                    _lastGlueStatus = glueStatus;
                }
                else
                    Profile.Status = "Waiting for server to come back online";
                _luaThrottleTimeStamp = DateTime.Now;
            }
        }

        private void AntiAfk()
        {
            if (WowHook != null)
                WowHook.Memory.Write<int>(true, Environment.TickCount, (IntPtr)HbRelogManager.Settings.LastHardwareEventOffset);
        }

        // credits mnbvc for original version. modified to work with Cata
        // http://www.ownedcore.com/forums/world-of-warcraft/world-of-warcraft-bots-programs/wow-memory-editing/302552-lua-auto-login-final-solution.html
        // indexes are {0}=BnetEmail, {1}=password, {2}=accountName

        private void UpdateLoginString()
        {
            string bnetLogin = Settings.Login.EncodeToUTF8();
            string accountName = Settings.AcountName;
            string password = Settings.Password.EncodeToUTF8();
            string server = Settings.ServerName;
            string character = Settings.CharacterName;
            // indexes are 0=BnetEmail, 1=password, 2=accountName, 3=character, 4=server
            _loginLua = string.Format(LoginLuaFormat, bnetLogin, password, accountName);
            // indexes are {0}=character, {1}=server
            _charSelectLua = string.Format(CharSelectLuaFormat, character, server);
            // indexes are {0}=server
            _realmSelectLua = string.Format(RealmSelectLuaFormat, server);
        }

        /// <summary>
        /// Scans for new memory offsets and saves them in WoWSettings. 
        /// </summary>
        private void ScanForOffset()
        {
            if (Memory != null)
            {
                HbRelogManager.Settings.GameStateOffset = (uint) WoWPatterns.GameStatePattern.Find(Memory);
                Log.Debug("GameState Offset found at 0x{0:X}", HbRelogManager.Settings.GameStateOffset);
                HbRelogManager.Settings.FrameScriptExecuteOffset = (uint)WoWPatterns.FrameScriptExecutePattern.Find(Memory);
                Log.Debug("FrameScriptExecute Offset found at 0x{0:X}", HbRelogManager.Settings.FrameScriptExecuteOffset);
                HbRelogManager.Settings.LastHardwareEventOffset = (uint)WoWPatterns.LastHardwareEventPattern.Find(Memory);
                Log.Debug("LastHardwareEvent Offset found at 0x{0:X}", HbRelogManager.Settings.LastHardwareEventOffset);
                HbRelogManager.Settings.GlueStateOffset = (uint)WoWPatterns.GlueStatePattern.Find(Memory);
                Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HbRelogManager.Settings.GlueStateOffset);
                HbRelogManager.Settings.WowVersion = GameProcess.VersionString();
                HbRelogManager.Settings.Save();
            }
            else
                MessageBox.Show("Can not scan for offsets before attaching to process");
        }

        #region Embeded Types

        // incomplete. missing Server Queue (if there is one).
        public enum GlueState
        {
            None = -1,
            Disconnected = 0,
            Updater,
            CharacterSelection = 2,
            CharacterCreation = 3,
            ServerSelection = 6,
            Credits = 7,
            RegionalSelection = 8
        }

        public static class WowStartupManager
        {
            private static readonly object LockObject = new object();
            private static readonly Dictionary<string, DateTime> TimeStamps = new Dictionary<string, DateTime>();

            public static bool CanStart(string path)
            {
                string key = path.ToUpper();
                lock (LockObject)
                {
                    if (TimeStamps.ContainsKey(key) &&
                        DateTime.Now - TimeStamps[key] < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay))
                    {
                        return false;
                    }
                    TimeStamps[key] = DateTime.Now;
                }
                return true;
            }
        }

        #endregion
    }
}