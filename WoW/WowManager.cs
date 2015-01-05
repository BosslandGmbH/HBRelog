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
using HighVoltz.HBRelog.WoW.Lua;
using HighVoltz.HBRelog.WoW.States;
using Region = HighVoltz.HBRelog.WoW.FrameXml.Region;

namespace HighVoltz.HBRelog.WoW
{
    public sealed class WowManager : Engine, IGameManager
    {
        public WowManager(CharacterProfile profile)
        {
            Profile = profile;
            States = new List<State> 
            {
                new StartWowState(this),
                new ScanOffsetsState(this),
                new WowWindowPlacementState(this),
                new LoginWowState(this),
                new RealmSelectState(this),
                new CharacterSelectState(this),
                new CharacterCreationState(this),
                new MonitorState(this),
            };
        }

        #region Fields

        private readonly object _lockObject = new object();
        internal readonly Stopwatch LoginTimer = new Stopwatch();

        private bool _isExiting;
        private GlueState _lastGlueStatus = GlueState.None;
        private DateTime _throttleTimeStamp = DateTime.Now;
        internal bool ProcessIsReadyForInput;
        private CharacterProfile _profile;

        private int _windowCloseAttempt;
        private Timer _wowCloseTimer;

        internal const int LuaStateGlobalsOffset = 0x50;

        #endregion

        #region Properties

        public WowSettings Settings { get; private set; }

        public ExternalProcessReader Memory { get; internal set; }

        public Process GameProcess { get; internal set; }

        private LuaTable _globals;
        public LuaTable Globals
        {
            get
            {
                if (Memory == null)
                    return null;
				var luaStatePtr = Memory.Read<IntPtr>((IntPtr)HbRelogManager.Settings.LuaStateOffset, true);
	            if (luaStatePtr == IntPtr.Zero)
	            {
#if DEBUG
					Log.Write("Lua state is not initialized");
#endif
		            return null;
	            }

				var globalsOffset = Memory.Read<IntPtr>(luaStatePtr + LuaStateGlobalsOffset);
	            if (globalsOffset == IntPtr.Zero)
	            {
#if DEBUG
					Log.Write("Lua globals is not initialized");
#endif
		            return null;
	            }
                if (_globals == null || _globals.Address != globalsOffset)
                    _globals = new LuaTable(Memory, globalsOffset);

                return _globals;
            }
        }

        public WowLockToken LockToken { get; internal set; }

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
                if (ServerIsOnline && !ServerHasQueue)
                {
                    GlueState glueStatus = GlueStatus;
                    // check if at server selection for tooo long.
                    if (glueStatus == _lastGlueStatus)
                    {
                        if (!LoginTimer.IsRunning)
                            LoginTimer.Start();

                        // check once every 40 seconds
                        if (LoginTimer.ElapsedMilliseconds > 40000)
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

        public bool ServerHasQueue
        {
            get
            {
                try
                {
                    if (InGame)
                        return false;
                    var button = UIObject.GetUIObjectByName<Button>(this, "GlueDialogButton1");
                    if (button != null && button.IsVisible)
                    {
                        var localizedChangeRealmText = Globals.GetValue("CHANGE_REALM");
                        return localizedChangeRealmText != null && button.Text == localizedChangeRealmText.String.Value;
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
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
                    if (Memory == null)
                        return false;

                    var state = Memory.Read<byte>(true, (IntPtr)HbRelogManager.Settings.GameStateOffset);
                    return state == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool StartupSequenceIsComplete { get; internal set; }
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
                StartupSequenceIsComplete = false;
                if (LockToken != null)
                {
                    LockToken.Dispose();
                    LockToken = null;
                }
            }
            if (lockAquried)
                Monitor.Exit(_lockObject);
        }

        override public void Pulse()
        {
            lock (_lockObject)
            {
                base.Pulse();
            }
        }

        #endregion

        #region Functions

        public void SetStartupSequenceToComplete()
        {
            StartupSequenceIsComplete = true;
            Profile.Log("Login sequence complete");
            Profile.Status = "Logged into WoW";
            if (OnStartupSequenceIsComplete != null)
                OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
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
                if (Memory != null)
                    CloseGameProcess(Process.GetProcessById(Memory.Process.Id));
            }
            //Profile.TaskManager.HonorbuddyManager.CloseBotProcess();
            GameProcess = null;
        }

        private void CloseGameProcess(Process proc)
        {
            if (!_isExiting && proc != null && !proc.HasExitedSafe())
            {
                _isExiting = true;
                Profile.Log("Attempting to close Wow");
                proc.CloseMainWindow();
                _windowCloseAttempt++;
                _wowCloseTimer = new Timer(
                    state =>
                    {
                        if (!((Process)state).HasExitedSafe())
                        {
	                        if (_windowCloseAttempt++ < 6)
	                        {
		                        proc.CloseMainWindow();
	                        }
	                        else
	                        {
		                        try
		                        {
			                        Profile.Log("Killing Wow");
			                        ((Process) state).Kill();
		                        }
		                        catch {}
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

        internal PointF ConvertWidgetCenterToWin32Coord(Region widget)
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


        #endregion
    }
}