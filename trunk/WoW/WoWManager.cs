using System;
using System.Diagnostics;
using HighVoltz.Settings;
using Magic;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace HighVoltz.WoW
{
    sealed public class WowManager : IGameManager
    {
        public WowManager(CharacterProfile profile)
        {
            Profile = profile;
        }

        public WowSettings Settings { get; private set; }
        CharacterProfile _profile;
        public CharacterProfile Profile
        {
            get { return _profile; }
            private set { _profile = value; Settings = value.Settings.WowSettings; }
        }
        public BlackMagic Memory { get { return WowHook != null ? WowHook.Memory : null; } }
        public Process GameProcess { get; private set; }

        public Lua Lua { get; set; }
        public Hook WowHook { get; private set; }
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
                        Memory.ReadByte(HBRelog.Settings.GameStateOffset + WowHook.BaseOffset) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }
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
                        Memory.ReadByte((HBRelog.Settings.GameStateOffset + 1) + WowHook.BaseOffset) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public GlueState GlueStatus
        {
            get { return WowHook != null ? (GlueState)Memory.ReadInt(HBRelog.Settings.GlueStateOffset + WowHook.BaseOffset) : GlueState.Disconnected; }
        }
        public bool IsRunning { get; private set; }
        public bool StartupSequenceIsComplete { get; private set; }
        bool _processIsReadyForInput;
        Timer _wowLoginTimer;
        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        /// <summary>
        /// Starts the WoW process
        /// </summary>
        void StartWoW()
        {
            Profile.Log("starting {0}", Settings.WowPath);
            Profile.Status = "Starting WoW";
            _processIsReadyForInput = StartupSequenceIsComplete = false;
            WowHook = null;
            Lua = null;
            GameProcess = Process.Start(Settings.WowPath);
            _wowLoginTimer = new Timer(WowLoginTimerCallBack, null, 0, 10000);
        }

        void WowLoginTimerCallBack(object state)
        {
            try
            {
                // once hook to endscene is removed then we can dispose this timer and check for crashes from the main thread.
                if (!IsRunning || StartupSequenceIsComplete)
                    _wowLoginTimer.Dispose();
                else if (!Profile.IsPaused && !WoWIsResponding || WowHasCrashed)
                {
                    Profile.Log("WoW has disconnected or crashed.. So lets restart WoW");
                    Profile.Status = "WoW has DCed or crashed. restarting";
                    KillGameProcess();
                    _wowLoginTimer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        void KillGameProcess()
        {
            try
            {
                if (GameProcess != null && !GameProcess.HasExited)
                    GameProcess.Kill();
            }
            // handle the "No process is associated with this object' exception while wow process is still 'active'
            catch (InvalidOperationException ex)
            {
                Log.Err(ex.ToString());
                if (WowHook != null)
                {
                    Process proc = Process.GetProcessById(WowHook.ProcessID);
                    if (proc != null)
                    {
                        proc.Kill();
                    }
                }
            }
        }

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
                    throw new InvalidOperationException(string.Format("path to WoW.exe does not exist: {0}", Settings.WowPath));
            }
        }
        object _lockObject = new object();
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
                KillGameProcess();
                GameProcess = null;
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
                    if (GameProcess.HasExited)
                    {
                        Profile.Log("WoW process was terminated. Restarting");
                        Profile.Status = "WoW process was terminated. Restarting";
                        StartWoW();
                        return;
                    }
                    // return if wow isn't ready for input.
                    if (!GameProcess.WaitForInputIdle(0))
                        return;
                    if (WowHook == null)
                    {  // resize and position window.
                        if (Settings.WowWindowWidth > 0 && Settings.WowWindowHeight > 0)
                        {
                            Profile.Log("Setting Window location to X:{0}, Y:{1} and Size to Width {2}, Height:{3}",
                                Settings.WowWindowX, Settings.WowWindowY,
                                Settings.WowWindowWidth, Settings.WowWindowHeight);

                            Utility.ResizeAndMoveWindow(GameProcess.MainWindowHandle, Settings.WowWindowX, Settings.WowWindowY,
                                Settings.WowWindowWidth, Settings.WowWindowHeight);
                        }
                        WowHook = new Hook(GameProcess);

                    }
                    if (!StartupSequenceIsComplete && !InGame && !IsConnectiongOrLoading)
                    {
                        if (!WowHook.Installed)
                        {
                            Profile.Log("Installing Endscene hook");
                            Profile.Status = "Logging into WoW";
                            WowHook.InstallHook();
                            Lua = new Lua(WowHook);
                            UpdateLoginString();
                        }
                        else if (!_processIsReadyForInput)
                            _processIsReadyForInput = true;
                        LoginWoW();
                    }
                    // remove hook since its nolonger needed.
                    if (WowHook.Installed && (InGame || IsConnectiongOrLoading))
                    {
                        Profile.Log("Login sequence complete. Removing hook");
                        Profile.Status = "Logged into WoW";
                        WowHook.DisposeHooking();
                        StartupSequenceIsComplete = true;
                        if (OnStartupSequenceIsComplete != null)
                            OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
                    }
                    // if WoW has disconnected or crashed close wow and start the login sequence again.
                    if ((StartupSequenceIsComplete && GlueStatus == GlueState.Disconnected) || !WoWIsResponding || WowHasCrashed)
                    {
                        Profile.Log("WoW has disconnected or crashed.. So lets restart WoW");
                        Profile.Status = "WoW has DCed or crashed. restarting";
                        KillGameProcess();
                        StartWoW();
                    }
                }
            }
        }

        Stopwatch _serverSelectionSW = new Stopwatch();
        DateTime _luaThrottleTimeStamp = DateTime.Now;
        GlueState _lastGlueStatus;
        private void LoginWoW()
        {
            // throttle lua calls to once per 3 secs, will give the user an option to change this.
            if (DateTime.Now - _luaThrottleTimeStamp >= TimeSpan.FromSeconds(3))
            {
                GlueState glueStatus = GlueStatus;
                // check if at server selection for tooo long.
                if (glueStatus == _lastGlueStatus)
                {
                    if (!_serverSelectionSW.IsRunning)
                        _serverSelectionSW.Start();
                    // check once every 20 seconds
                    if (_serverSelectionSW.ElapsedMilliseconds > 20000)
                    {
                        Profile.Log("Failed to login wow, lets restart");
                        GameProcess.Kill();
                        StartWoW();
                        return;
                    }
                }
                else if (_serverSelectionSW.IsRunning)
                    _serverSelectionSW.Reset();

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
                        Lua.DoString("if (CharCreateRandomizeButton and CharCreateRandomizeButton:IsVisible()) then CharacterCreate_Back() end ");
                        break;
                    case GlueState.Updater:
                        Profile.Pause();
                        Profile.Log("Wow is updating. pausing.");
                        break;
                }
                Profile.Log("GlueStatus: {0}", GlueStatus);
                _luaThrottleTimeStamp = DateTime.Now;
                _lastGlueStatus = glueStatus;
            }
        }
        Stopwatch _wowRespondingSW = new Stopwatch();
        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        public bool WoWIsResponding
        {
            get
            {
                try
                {
                    if (GameProcess.HasExited)
                        return false;
                    bool isResponding = GameProcess.Responding;
                    if (!isResponding && !_wowRespondingSW.IsRunning)
                        _wowRespondingSW.Start();
                    if (_wowRespondingSW.ElapsedMilliseconds >= 10000 && !isResponding)
                        return false;
                    else if (isResponding && _wowRespondingSW.IsRunning)
                        _wowRespondingSW.Reset();
                }
                catch (InvalidOperationException)
                {
                    if (_processIsReadyForInput)
                        return false;
                }
                return true;
            }
        }

        DateTime _crashTimeStamp = DateTime.Now;
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
                        foreach (IntPtr hnd in childWinHandles)
                        {
                            string caption = NativeMethods.GetWindowText(hnd);
                            if (caption == "Wow")
                            {
                                return true;
                            }
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

        void AntiAfk()
        {
            if (WowHook != null)
                WowHook.Memory.WriteInt(HBRelog.Settings.LastHardwareEventOffset + WowHook.BaseOffset, System.Environment.TickCount);
        }
        // credits mnbvc for original version. modified to work with Cata
        // http://www.ownedcore.com/forums/world-of-warcraft/world-of-warcraft-bots-programs/wow-memory-editing/302552-lua-auto-login-final-solution.html
        // indexes are {0}=BnetEmail, {1}=password, {2}=accountName

        const string LoginLuaFormat =
            "local acct = \"{2}\" " +
            "if (WoWAccountSelectDialog and WoWAccountSelectDialog:IsShown()) then " +
                "for i = 1, GetNumGameAccounts() do " +
                    "if GetGameAccountInfo(i):upper() == acct:upper() then " +
                        "WoWAccountSelect_SelectAccount(i) " +
                    "end " +
                "end " +
            "elseif (AccountLoginUI and AccountLoginUI:IsVisible()) then " +
                "if (AccountLoginDropDown:IsShown()) then " +
                   " for i=1, #AccountList  do " +
                        "if AccountList[i].text:upper() == acct:upper() then " +
                            "GlueDropDownMenu_SetSelectedName(AccountLoginDropDown,AccountList[i].text) " +
                            "GlueDialog_Show('ACCOUNT_MSG',AccountList[i].text) " +
                        "end " +
                    "end  " +
                "end " +
                "DefaultServerLogin(\"{0}\",\"{1}\") " +
                "AccountLoginUI:Hide() " +
            "end ";

        // indexes are {0}=character, {1}=server
        const string CharSelectLuaFormat =
            "local name = \"{0}\" " +
            "local server = \"{1}\" " +
            "if (CharacterSelectUI and CharacterSelectUI:IsVisible()) then " +
                "if GetServerName():upper() ~= server:upper() and (not RealmList or not RealmList:IsVisible()) then " +
                    "RequestRealmList(1) " +
                "else " +
                    "for i = 1,GetNumCharacters() do " +
            //"local name = GetCharacterInfo(i) " +
            //"GlueDialog_Show('ACCOUNT_MSG',name:upper()..':'..'" + character + "') " + 
                        "if (GetCharacterInfo(i):upper() == name:upper()) then " +
                            "CharacterSelect_SelectCharacter(i) " +
                            "CharSelectEnterWorldButton:Click() " +
                        "end " +
                    "end " +
                "end " +
            "elseif (CharCreateRandomizeButton and CharCreateRandomizeButton:IsVisible()) then " +
                "CharacterCreate_Back() " +
            "end ";

        // indexes are {0}=server
        const string RealmSelectLuaFormat =
            "local server = \"{0}\" " +
            "if (RealmList and RealmList:IsVisible()) then " +
                "for i = 1, select('#',GetRealmCategories()) do " +
                    "for j = 1, GetNumRealms(i) do " +
                        "if GetRealmInfo(i, j):upper() == server:upper() then " +
                            "RealmList:Hide() " +
                            "ChangeRealm(i, j) " +
                        "end " +
                    "end " +
                "end " +
            "end ";

        private string _loginLua;
        private string _charSelectLua;
        private string _realmSelectLua;

        private void UpdateLoginString()
        {
            string bnetLogin = Settings.Login;
            string accountName = Settings.AcountName;
            string password = Settings.Password;
            string server = Settings.ServerName;
            string character = Settings.CharacterName;
            // indexes are 0=BnetEmail, 1=password, 2=accountName, 3=character, 4=server
            _loginLua = string.Format(LoginLuaFormat, bnetLogin, password, accountName);
            // indexes are {0}=character, {1}=server
            _charSelectLua = string.Format(CharSelectLuaFormat, character, server);
            // indexes are {0}=server
            _realmSelectLua = string.Format(RealmSelectLuaFormat, server);
        }

        #region Embeded Types
        // incomplete. missing Server Queue (if there is one).
        public enum GlueState
        {
            Disconnected = 0,
            Updater,
            CharacterSelection = 2,
            CharacterCreation = 3,
            ServerSelection = 6,
            Credits = 7,
            RegionalSelection = 8
        }
        #endregion

    }
}
