using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
		private GlueScreen _lastGlueScreen = GlueScreen.None;
		private DateTime _throttleTimeStamp = DateTime.Now;
		internal bool ProcessIsReadyForInput;
		private CharacterProfile _profile;

		internal const int LuaStateGlobalsOffset = 0x50;

		#endregion

		#region Properties

		public WowSettings Settings { get; private set; }

        public ExternalProcessReader Memory { get; internal set; }

        public IntPtr GameWindow { get; internal set; }

        public Process GameProcess => Memory?.Process;

        public int GameProcessId { get; internal set; }

        public string GameProcessName { get; internal set; }

        public LuaTable Globals
		{
			get
			{
				if (Memory == null)
					return null;
				var luaStatePtr = Memory.Read<IntPtr>((IntPtr) HbRelogManager.Settings.LuaStateOffset, true);
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
				return new LuaTable(Memory, globalsOffset);
			}
		}

        public LuaTValue GetLuaObject(string luaAccessorCode)
        {
            LuaTable curTable = Globals;
            string[] split = luaAccessorCode.Split('.');
            for (int i = 0; i < split.Length - 1; i++)
            {
                if (curTable == null)
                    return null;

                LuaTValue val = curTable.GetValue(split[i]);
                if (val == null || val.Type != LuaType.Table)
                    return null;

                curTable = val.Table;
            }

            return curTable.GetValue(split.Last());
        }

        public WowLockToken LockToken { get; internal set; }

		public IntPtr FocusedWidgetPtr
			=> Memory?.Read<IntPtr>((IntPtr) HbRelogManager.Settings.FocusedWidgetOffset, true) ?? IntPtr.Zero;

		public UIObject FocusedWidget
		{
			get
			{
				var widgetAddress = FocusedWidgetPtr;
				return widgetAddress != IntPtr.Zero ? UIObject.GetUIObjectFromPointer(this, widgetAddress) : null;
			}
		}

		/// <summary>WoW is at the connecting or loading screen</summary>
		public bool IsConnectingOrLoading
		{
			get
			{
				try
				{
					return Memory != null && (Memory.Read<byte>(true, ((IntPtr) HbRelogManager.Settings.GameStateOffset)) & 1) != 0;
				}
				catch
				{
					return false;
				}
			}
		}



		public GlueScreen GlueScreen
		{
			get
			{
				if (Memory == null)
					return GlueScreen.None;

				LuaTValue secondary = GetLuaObject("GlueParent.currentSecondaryScreen");
				if (secondary != null && secondary.Type == LuaType.String)
				{
					switch (secondary.String.Value)
					{
						case "cinematics":
							return GlueScreen.Cinematics;
						case "movie":
							return GlueScreen.Movie;
						case "credits":
							return GlueScreen.Credits;
						case "options":
							return GlueScreen.Options;
					}
				}

				LuaTValue primary = GetLuaObject("GlueParent.currentScreen");
				if (primary != null && primary.Type == LuaType.String)
				{
					switch (primary.String.Value)
					{
						case "login":
							return GlueScreen.Login;
						case "realmlist":
							return GlueScreen.RealmList;
						case "charselect":
							return GlueScreen.CharSelect;
						case "charcreate":
							return GlueScreen.CharCreate;
					}
				}

				return GlueScreen.None;
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
			get
			{
				return !HbRelogManager.Settings.CheckRealmStatus ||
				       HbRelogManager.WowRealmStatus.RealmIsOnline(Settings.ServerName, Settings.Region);
			}
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

				if (!LoginHasQueue && ServerIsOnline && !ServerHasQueue)
				{
					GlueScreen glueStatus = GlueScreen;
					// check if at server selection for tooo long.
					if (glueStatus == _lastGlueScreen)
					{
						if (!LoginTimer.IsRunning)
							LoginTimer.Start();

						// check once every 40 seconds
						if (LoginTimer.ElapsedMilliseconds > 40000)
						{
							_lastGlueScreen = GlueScreen.None;
							return true;
						}
					}
					else if (LoginTimer.IsRunning)
						LoginTimer.Reset();
					_lastGlueScreen = glueStatus;
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
                        var localizedChangeRealmText = GetLuaObject("CHANGE_REALM");
                        return localizedChangeRealmText != null &&
                               button.Text == localizedChangeRealmText.String.Value;
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private Regex _bnetLoginQueueTimeLeftSecondsRegEx;
        private Regex _bnetLoginQueueTimeLeftUnknownRegEx;
        private Regex _bnetLoginQueueTimeLeftRegEx;

        public bool LoginHasQueue
        {
            get
            {
                try
                {
                    if (InGame)
                        return false;

                    var glueDialogTextContol = UIObject.GetUIObjectByName<FontString>(this, "GlueDialogText");
                    if (glueDialogTextContol == null || !glueDialogTextContol.IsVisible)
                        return false;

                    if (_bnetLoginQueueTimeLeftSecondsRegEx == null)
                        _bnetLoginQueueTimeLeftSecondsRegEx = GetLoginQueueRegEx("BNET_LOGIN_QUEUE_TIME_LEFT_SECONDS");
                    if (_bnetLoginQueueTimeLeftSecondsRegEx.IsMatch(glueDialogTextContol.Text))
                        return true;

                    if (_bnetLoginQueueTimeLeftUnknownRegEx == null)
                        _bnetLoginQueueTimeLeftUnknownRegEx = GetLoginQueueRegEx("BNET_LOGIN_QUEUE_TIME_LEFT_UNKNOWN");
                    if (_bnetLoginQueueTimeLeftUnknownRegEx.IsMatch(glueDialogTextContol.Text))
                        return true;

                    if (_bnetLoginQueueTimeLeftRegEx == null)
                        _bnetLoginQueueTimeLeftRegEx = GetLoginQueueRegEx("BNET_LOGIN_QUEUE_TIME_LEFT");
                    if (_bnetLoginQueueTimeLeftRegEx.IsMatch(glueDialogTextContol.Text))
                        return true;

                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

	    private Regex GetLoginQueueRegEx(string globalVarName)
	    {
	        var text = GetLuaObject(globalVarName).String.Value;
            return new Regex(text.Replace("%d", "\\d+"));
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

		/// <summary>Character is logged in game</summary>
		public bool InGame
		{
			get
			{
				try
				{
					if (Memory == null)
						return StartupSequenceIsComplete;

					var state = Memory.Read<byte>(true, (IntPtr) HbRelogManager.Settings.GameStateOffset);
					var loadingScreenCount = Memory.Read<int>(
						true,
						(IntPtr) HbRelogManager.Settings.LoadingScreenEnableCountOffset);
					return (state & 2) != 0 && loadingScreenCount == 0;
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
			var lockAquried = Monitor.TryEnter(_lockObject, 500);
			if (IsRunning)
			{
                CloseGameProcess();
                if (Memory != null)
                {
                    Memory.Dispose();
                    Memory = null;
                }
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

            Profile.Status = "Stopped";
        }

        public override void Pulse()
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
            OnStartupSequenceIsComplete?.Invoke(this, new ProfileEventArgs(Profile));
        }

		public void CloseGameProcess()
		{
            if (GameProcessId <= 0)
                return;

            var procInfo = new ProcessStartInfo("taskkill", $"/F /PID {GameProcessId}") { CreateNoWindow = true, UseShellExecute  = false};
            Process.Start(procInfo);
            Profile.Log("Killing Wow");
            GameProcessId = 0;
            StartupSequenceIsComplete = false;
        }

		internal PointF ConvertWidgetCenterToWin32Coord(Region widget)
		{
			var ret = new PointF();
			var gameFullScreenFrame = UIObject.GetUIObjectByName<Frame>(this, "GlueParent") ??
			                          UIObject.GetUIObjectByName<Frame>(this, "UIParent");
			if (gameFullScreenFrame == null)
				return ret;
			var gameFullScreenFrameRect = gameFullScreenFrame.Rect;
			var widgetCenter = widget.Center;
			var windowInfo = Utility.GetWindowInfo(GameWindow);
			var leftBorderWidth = windowInfo.rcClient.Left - windowInfo.rcWindow.Left;
			var bottomBorderWidth = windowInfo.rcWindow.Bottom - windowInfo.rcClient.Bottom;
			var winClientWidth = windowInfo.rcClient.Right - windowInfo.rcClient.Left;
			var winClientHeight = windowInfo.rcClient.Bottom - windowInfo.rcClient.Top;
            
            // gameFullScreenFrameRect sometimes doesn't fill entire screen.
            // When that happens, it's centered on the screen, and we need to add the gaps that it doesn't occupy.
            // Left gap is simply gameFullScreenFrameRect.Left, and we assume the right side gap is same width because frame is centered, so we just multiply left gap by 2.

            var xCo = winClientWidth/(gameFullScreenFrameRect.Width + gameFullScreenFrameRect.Left * 2);
			var yCo = winClientHeight/gameFullScreenFrameRect.Height + gameFullScreenFrameRect.Top * 2;

			ret.X = widgetCenter.X * xCo + leftBorderWidth;
			ret.Y = widgetCenter.Y * yCo + bottomBorderWidth;
			// flip the Y coord around because in WoW's UI coord space the Y goes up where as in windows it goes down.
			ret.Y = windowInfo.rcWindow.Bottom - windowInfo.rcWindow.Top - ret.Y;
			return ret;
		}

        public bool WaitForMessageHandler(int timeout)
        {
            IntPtr handle = GameWindow;
            UIntPtr result;
            IntPtr response = NativeMethods.SendMessageTimeout(handle, (uint)NativeMethods.Message.WM_NULL,
                                                               IntPtr.Zero,
                                                               UIntPtr.Zero,
                                                               NativeMethods.SendMessageTimeoutFlags
                                                                            .SMTO_ABORTIFHUNG,
                                                               (uint)timeout, out result);

            return response != IntPtr.Zero;
        }
        #endregion

        #region Embeded Types

        #endregion
    }

	// incomplete. missing Server Queue (if there is one).
	public enum GlueScreen
	{
		None,
		Login,
		RealmList,
		CharSelect,
		CharCreate,

		Cinematics,
		Credits,
		Movie,
		Options
	}
}