using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using GreyMagic;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.FiniteStateMachine.FiniteStateMachine;
using HighVoltz.HBRelog.Settings;
using HighVoltz.HBRelog.WoW.FrameXml;
using HighVoltz.HBRelog.WoW.States;
using Test.FrameXml;
using Test.Lua;
using Region = HighVoltz.HBRelog.WoW.FrameXml.Region;

namespace HighVoltz.HBRelog.WoW
{
    public sealed class WowManager : Engine, IGameManager
    {
        /* 
        private const string AcceptTosEulaLua = @"if TOSFrame and TOSFrame:IsShown() then
            AcceptTOS();
            AcceptEULA();
            AccountLoginUI:Show();
            TOSFrame:Hide();
        end";

        private const string LoginLuaFormat = @"
        local acct = ""{2}""
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
        private const string CharSelectLuaFormat = @"local name = ""{0}""
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
        private const string RealmSelectLuaFormat = @"local server = ""{0}""
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
        */

        public WowManager(CharacterProfile profile)
        {
            Profile = profile;
            States = new List<State> 
            {
                new StartWowState(this),
                new InitializeMemoryState(this),
                new ScanOffsetsState(this),
                new WowWindowPlacementState(this),
                new LoginWowState(this),
            };
        }

        #region Fields

        private readonly object _lockObject = new object();
        private readonly Stopwatch _loggedOutSw = new Stopwatch();
        internal readonly Stopwatch LoginTimer = new Stopwatch();
        private readonly Stopwatch _wowRespondingSw = new Stopwatch();

        private DateTime _crashTimeStamp = DateTime.Now;
        private bool _isExiting;
        private GlueState _lastGlueStatus = GlueState.None;
        private DateTime _loggedoutTimeStamp = DateTime.Now;
        private DateTime _throttleTimeStamp = DateTime.Now;
        internal bool ProcessIsReadyForInput;
        private CharacterProfile _profile;

        private int _windowCloseAttempt;
        private Timer _wowCloseTimer;
        private bool _wowIsLoggedOutForTooLong;

        internal const int LuaStateGlobalsOffset = 0x50;
        internal int LauncherPid;

        #endregion

        #region Properties

        public WowSettings Settings { get; private set; }

        public ExternalProcessReader Memory { get; internal set; }

        public Process GameProcess { get; internal set; }

        public LuaTable Globals { get; internal set; }

        public IntPtr FocusedWidgetPtr
        {
            get { return Memory == null ? IntPtr.Zero : Memory.Read<IntPtr>((IntPtr)HbRelogManager.Settings.FocusedWidgetOffset, true); }
        }

        public UIObject FocusedWidget
        {
            get
            {
                IntPtr widgetAddress = FocusedWidgetPtr;
                return widgetAddress != IntPtr.Zero ? UIObject.GetUIObjectFromPointer(this, widgetAddress) : null;
            }
        }

        /// <summary>
        ///     WoW is at the connecting or loading screen
        /// </summary>
        public bool IsConnectiongOrLoading
        {
            get
            {
                try
                {
                    return Memory != null && Memory.Read<byte>(true, ((IntPtr)HbRelogManager.Settings.GameStateOffset + 1)) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public GlueState GlueStatus
        {
            get { return Memory != null ? Memory.Read<GlueState>(true, (IntPtr)HbRelogManager.Settings.GlueStateOffset) : GlueState.Disconnected; }
        }

        /// <summary>
        ///     returns false if the WoW user interface is not responsive for 10+ seconds.
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
                    if (ProcessIsReadyForInput)
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
                        if (ProcessIsReadyForInput)
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

        internal bool IsUsingLauncher
        {
            get
            {
                var fileName = Path.GetFileName(Settings.WowPath);
                return fileName != null && !fileName.Equals("Wow.exe", StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public bool ServerIsOnline
        {
            get { return !HbRelogManager.Settings.CheckRealmStatus || HbRelogManager.WowRealmStatus.RealmIsOnline(Settings.ServerName, Settings.Region); }
        }

        public bool Throttled
        {
            get
            {
                var time = DateTime.Now;
                var ret = time - _throttleTimeStamp < TimeSpan.FromSeconds(HbRelogManager.Settings.LoginDelay);
                if (!ret)
                    _throttleTimeStamp = time;
                return ret;
            }
        }

        public bool StalledLogin
        {
            get
            {
                if (Memory == null)
                    return false;
                if (ServerIsOnline)
                {
                    GlueState glueStatus = GlueStatus;
                    // check if at server selection for tooo long.
                    if (glueStatus == _lastGlueStatus)
                    {
                        if (!LoginTimer.IsRunning)
                            LoginTimer.Start();
                        
                        // check once every 40 seconds
                        if (LoginTimer.ElapsedMilliseconds > 40000 && !ServerHasQueue)
                        {                            
                            _lastGlueStatus = GlueState.None;                            
                            return true;
                        }
                    }
                    else if (LoginTimer.IsRunning)
                        LoginTimer.Reset();
                    _lastGlueStatus = glueStatus;
                }
                return false;
            }
        }

        // todo: implement
        public bool ServerHasQueue
        {
            get { return false; }
        }

        #endregion

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

        public void SetSettings(WowSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        ///     Character is logged in game
        /// </summary>
        public bool InGame
        {
            get
            {
                try
                {
                    return Memory != null && Memory.Read<byte>(true, (IntPtr)HbRelogManager.Settings.GameStateOffset) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool StartupSequenceIsComplete { get; private set; }
        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;

        public void Start()
        {
            lock (_lockObject)
            {
                if (File.Exists(Settings.WowPath))
                {
                    IsRunning = true;
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
                Memory = null;
                CloseGameProcess();
                IsRunning = false;
                LauncherPid = 0;
                StartupSequenceIsComplete = false;
            }
            if (lockAquried)
                Monitor.Exit(_lockObject);
        }

        override public void Pulse()
        {
            lock (_lockObject)
            {
                base.Pulse();
                return;
                // restart WoW if it has exited

                if (!StartupSequenceIsComplete)
                {
                    if (!InGame && !IsConnectiongOrLoading)
                    {
                        LoginWoW();
                    }
                    // remove hook since its nolonger needed.
                    if (InGame || IsConnectiongOrLoading)
                    {
                        Profile.Log("Login sequence complete");
                        Profile.Status = "Logged into WoW";
                        StartupSequenceIsComplete = true;
                        if (OnStartupSequenceIsComplete != null)
                            OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
                    }
                }

                // if WoW has disconnected or crashed close wow and start the login sequence again.

                if ((StartupSequenceIsComplete && (GlueStatus == GlueState.Disconnected || WowIsLoggedOutForTooLong)) || !WoWIsResponding || WowHasCrashed)
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

        #endregion

        #region Functions

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
                if (Memory != null)
                    CloseGameProcess(Process.GetProcessById(Memory.Process.Id));
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
                _wowCloseTimer = new Timer(
                    state =>
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
                    },
                    proc,
                    1000,
                    1000);
            }
        }

        internal PointF ConvertWidgetCenterToWin32Coord (Region widget)
        {
            var ret = new PointF();
            var gameFullScreenFrame = UIObject.GetUIObjectByName<Frame>(this, "GlueParent") ?? UIObject.GetUIObjectByName<Frame>(this, "UIParent");
            if (gameFullScreenFrame == null)
                return ret;
            var gameFullScreenFrameRect = gameFullScreenFrame.Rect;
            var widgetCenter = widget.Center;
            var windowInfo = Utility.GetWindowInfo(GameProcess.MainWindowHandle);
            var leftBorderWidth = windowInfo.rcClient.Left - windowInfo.rcWindow.Left;
            var bottomBorderWidth = windowInfo.rcWindow.Bottom - windowInfo.rcClient.Bottom;
            var winClientWidth = windowInfo.rcClient.Right - windowInfo.rcClient.Left;
            var winClientHeight = windowInfo.rcClient.Bottom - windowInfo.rcClient.Top;
            var xCo = winClientWidth / gameFullScreenFrameRect.Width;
            var yCo = winClientHeight / gameFullScreenFrameRect.Height;

            ret.X = widgetCenter.X * xCo + leftBorderWidth;
            ret.Y = widgetCenter.Y * yCo + bottomBorderWidth;
            // flip the Y coord around because in WoW's UI coord space the Y goes up where as in windows it goes down.
            ret.Y = windowInfo.rcWindow.Bottom - windowInfo.rcWindow.Top - ret.Y;
            return ret;
        }

        private void LoginWoW()
        {
            // throttle lua calls.
            if (DateTime.Now - _throttleTimeStamp >= TimeSpan.FromSeconds(HbRelogManager.Settings.LoginDelay))
            {
                bool serverIsOnline = !HbRelogManager.Settings.CheckRealmStatus ||
                                      (HbRelogManager.Settings.CheckRealmStatus && HbRelogManager.WowRealmStatus.RealmIsOnline(Settings.ServerName, Settings.Region));
                // if we are checking for wow server status and the wow server is offline then return
                if (serverIsOnline)
                {
                    GlueState glueStatus = GlueStatus;
                    // check if at server selection for tooo long.
                    if (glueStatus == _lastGlueStatus)
                    {
                        if (!LoginTimer.IsRunning)
                            LoginTimer.Start();
                        WowRealmStatus.WowRealmStatusEntry status = HbRelogManager.WowRealmStatus[Settings.ServerName, Settings.Region];
                        bool serverHasQueue = HbRelogManager.Settings.CheckRealmStatus && status != null && status.InQueue;
                        // check once every 40 seconds
                        if (LoginTimer.ElapsedMilliseconds > 40000 && !serverHasQueue)
                        {
                            Profile.Log("Failed to login wow, lets restart");
                            GameProcess.Kill();
                            // set to 'None' to prevent an infinite loop if set to 'Disconnected'
                            _lastGlueStatus = GlueState.None;
                            return;
                        }
                    }
                    else if (LoginTimer.IsRunning)
                        LoginTimer.Reset();

                    switch (glueStatus)
                    {
                        case GlueState.Disconnected:
                            Profile.Status = "Logging in";

                            break;
                        case GlueState.CharacterSelection:
                            Profile.Status = "At Character Selection";
                            //  Lua.DoString(_charSelectLua);
                            break;
                        case GlueState.ServerSelection:
                            Profile.Status = "At Server Selection";
                            // Lua.DoString(_realmSelectLua);
                            break;
                        case GlueState.CharacterCreation:
                            // Lua.DoString("CharacterCreate_Back()");
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
                _throttleTimeStamp = DateTime.Now;
            }
        }


        #endregion

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
                    if (TimeStamps.ContainsKey(key) && DateTime.Now - TimeStamps[key] < TimeSpan.FromSeconds(HbRelogManager.Settings.WowDelay))
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