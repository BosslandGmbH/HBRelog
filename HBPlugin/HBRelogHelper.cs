//!CompilerOption:Optimize:On
//!CompilerOption:AddRef:System.ServiceModel.dll
using HighVoltz.HBRelog.Remoting;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Plugins;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Styx.Common.Helpers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using Styx.WoWInternals;
using System.IO;

namespace HighVoltz.HBRelog.Remoting
{
    [ServiceContract]
    internal interface IRemotingApi
    {
        [OperationContract]
        bool Init(int hbProcId);

        [OperationContract(IsOneWay = true)]
        void Heartbeat(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void RestartHB(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void RestartWow(int hbProcID);

        [OperationContract]
        string[] GetProfileNames();

        [OperationContract]
        string GetCurrentProfileName(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void StartProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void StopProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void PauseProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void IdleProfile(string profileName, TimeSpan time);

        [OperationContract(IsOneWay = true)]
        void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath);

        [OperationContract]
        int GetProfileStatus(string profileName);

        [OperationContract(IsOneWay = true)]
        void SetProfileStatusText(int hbProcID, string status);

        [OperationContract(IsOneWay = true)]
        void ProfileLog(int hbProcID, string msg);


        [OperationContract(IsOneWay = true)]
        void SetBotInfoToolTip(int hbProcID, string tooltip);

        [OperationContract(IsOneWay = true)]
        void SkipCurrentTask(string profileName);
    }
}

namespace HighVoltz.HBRelogHelper
{
    public class HBRelogHelper : HBPlugin
    {

        #region Overrides

        public override string Author { get; } = "HighVoltz";


        public override string Name { get; } = "HBRelogHelper";

        public override void Pulse()
        {
        }

        public override Version Version { get; } =  new Version(1, 0);

        public override bool WantButton { get; } = false;

        public override void OnButtonPress()
        {
            Logging.Write("IsConnected: {0}", IsConnected);
            foreach (string name in HBRelogRemoteApi.GetProfileNames())
            {
                Logging.Write("{1}: GetProfileStatus: {0}", HBRelogRemoteApi.GetProfileStatus(name), name);
                HBRelogRemoteApi.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
            }
        }

        #endregion

        public static bool IsConnected { get; private set; }

        internal static IRemotingApi HBRelogRemoteApi { get; private set; }
        internal static int HbProcId { get; private set; }
        internal static string CurrentProfileName { get; private set; }

        private static DispatcherTimer _monitorTimer;

        //static IpcChannel _ipcChannel;
        private static ChannelFactory<IRemotingApi> _pipeFactory;

        static internal HBRelogHelper Instance { get; private set; }

        public HBRelogHelper()
        {
            Instance = this;
            try
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

                HbProcId = Process.GetCurrentProcess().Id;
                var pipeInfoPath = Path.Combine(Utilities.AssemblyDirectory, "Plugins", "HBRelogHelper", "pipeName.txt");
                var pipeName = File.ReadAllText(pipeInfoPath);
                _pipeFactory = new ChannelFactory<IRemotingApi>(new NetNamedPipeBinding(),
                        new EndpointAddress($"net.pipe://localhost/{pipeName}/Server"));
                HBRelogRemoteApi = _pipeFactory.CreateChannel();

                IsConnected = HBRelogRemoteApi.Init(HbProcId);
                if (IsConnected)
                {
                    Log("Connected with HBRelog");
                    //instead of spawning a new thread use the GUI one.
                    Application.Current.Dispatcher.Invoke(new Action(
                        delegate
                        {
                            _monitorTimer = new DispatcherTimer();
                            _monitorTimer.Tick += MonitorTimerCb;
                            _monitorTimer.Interval = TimeSpan.FromSeconds(10);
                            _monitorTimer.Start();
                        }));

                    CurrentProfileName = HBRelogRemoteApi.GetCurrentProfileName(HbProcId);
                }
                else
                {
                    Log("Could not connect to HBRelog");
                }
            }
            catch (Exception ex)
            {
                // fail silently.
                Logging.Write(Colors.Red, ex.ToString());
            }
        }

        private void Shutdown()
        {
            try
            {
                if (_pipeFactory.State == CommunicationState.Opened || _pipeFactory.State == CommunicationState.Opening)
                {
                    _pipeFactory.Close();
                    _pipeFactory.Abort();
                }
            }
            catch
            { }
        }

        private void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Shutdown();
        }

        private void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            Shutdown();
        }

        private static string _lastStatus;
        private static string _lastTooltip;
        private static readonly WaitTimer HeartbeatTimer = new WaitTimer(TimeSpan.FromSeconds(30));

        public static void MonitorTimerCb(object sender, EventArgs args)
        {
            try
            {
                if (!IsConnected || ProfileState != ProfileState.Running)
                    return;

                if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
                {
                    HBRelogApi.SetProfileStatusText(TreeRoot.StatusText);
                    _lastStatus = TreeRoot.StatusText;
                }

                if (HeartbeatTimer.IsFinished)
                {
                    HBRelogRemoteApi.Heartbeat(HbProcId);
                    HeartbeatTimer.Reset();
                }

                CheckWowHealth();
                if (GameStats.IsMeasuring)
                    UpdateTooltip();
            }
            catch (Exception ex)
            {
                if (ex is CommunicationObjectFaultedException)
                    return;
                if (ex is EndpointNotFoundException)
                    Log("Unable to connect to HBRelog");
                Logging.WriteException(ex);
            }
        }

        private static void CheckWowHealth()
        {
            var wowProblem = FindWowProblem();
            if (wowProblem == WowProblem.None)
                return;

            switch (wowProblem)
            {
                case WowProblem.Disconnected:
                    HBRelogApi.ProfileLog("WoW has disconnected.. So lets restart WoW");
                    HBRelogApi.SetProfileStatusText("WoW has DCed. restarting");
                    break;
                case WowProblem.LoggedOutForTooLong:
                    HBRelogApi.ProfileLog("Restarting wow because it was logged out for more than 40 seconds");
                    HBRelogApi.SetProfileStatusText("WoW was logged out for too long. restarting");
                    break;
            }

            TreeRoot.Shutdown(HonorbuddyExitCode.Default, true);
        }

        private static void UpdateTooltip()
        {
            try
            {
                string tooltip = string.Empty;
                if (StyxWoW.Me.Level < 110)
                    tooltip += string.Format("XP/hr: {0}\n", GameStats.XPPerHour);

                if (TreeRoot.Current.Name == "BGBuddy")
                {
                    tooltip += string.Format("BGs: {0} ({1}/hr)\n",
                        GameStats.BGsCompleted, GameStats.BGsPerHour);
                    tooltip += string.Format("BGs won: {0} ({1}/hr)\n",
                        GameStats.BGsWon, GameStats.BGsWonPerHour);
                    tooltip += string.Format("BGs lost: {0} ({1}/hr)\n",
                        GameStats.BGsLost, GameStats.BGsLostPerHour);
                    tooltip += string.Format("Honor/hr: {0}\n", GameStats.HonorPerHour);
                }
                else
                {
                    tooltip += $"Gold: {GameStats.GoldGained} ({GameStats.LootsPerHour}/hr)\n";
                    tooltip += string.Format("Loots: {0} ({1}/hr)\n",
                        GameStats.Loots, GameStats.LootsPerHour);
                    tooltip += string.Format("Deaths: {0} - ({1}/hr)\n",
                        GameStats.Deaths, GameStats.DeathsPerHour);
                    if (TreeRoot.Current.Name != "Gatherbuddy2")
                    {
                        tooltip += string.Format("Mobs killed: {0} - ({1}/hr)\n",
                        GameStats.MobsKilled, GameStats.MobsPerHour);
                    }
                }
                if (tooltip != _lastTooltip)
                {
                    HBRelogRemoteApi.SetBotInfoToolTip(HbProcId, tooltip);
                    _lastTooltip = tooltip;
                }
            }
            catch (Exception ex)
            {
                // Logging.WriteException(ex);
            }
        }

        private static WowProblem FindWowProblem()
        {
            if (GlueScreen == GlueScreen.Login)
                return WowProblem.Disconnected;

            if (WowIsLoggedOutForTooLong)
                return WowProblem.LoggedOutForTooLong;

            return WowProblem.None;
        }

        private static Stopwatch _loggedOutTimer;
        private static bool WowIsLoggedOutForTooLong
        {
            get
            {
                if (!StyxWoW.IsInGame)
                {
                    if (_loggedOutTimer == null)
                        _loggedOutTimer = Stopwatch.StartNew();
                }
                else if (_loggedOutTimer != null)
                {
                    _loggedOutTimer = null;
                }
                return _loggedOutTimer != null && _loggedOutTimer.Elapsed >= TimeSpan.FromMinutes(2);
            }
        }

        private static ProfileState ProfileState => (ProfileState) HBRelogApi.GetProfileStatus(HBRelogApi.CurrentProfileName);

        private static void Log(string msg)
        {
            Logging.Write("HBRelogHelper: " + msg);
        }


        internal static LuaTValue GetLuaObject(string luaAccessorCode)
        {
            LuaTable curTable = Lua.State.Globals;
            string[] split = luaAccessorCode.Split('.');
            for (int i = 0; i < split.Length - 1; i++)
            {
                if (curTable == null)
                    return null;

                LuaTValue val = curTable.GetField(split[i]);
                if (val == null || val.Type != LuaType.Table)
                    return null;

                curTable = val.Value.Table;
            }

            return curTable.GetField(split.Last());
        }

        private static GlueScreen GlueScreen
        {
            get
            {
                LuaTValue secondary = GetLuaObject("GlueParent.currentSecondaryScreen");
                if (secondary != null && secondary.Type == LuaType.String)
                {
                    switch (secondary.Value.String.Value)
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
                    switch (primary.Value.String.Value)
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

    }

    internal enum GlueScreen
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


    internal enum WowProblem
    {
        None,
        Disconnected,
        LoggedOutForTooLong,
    }

    internal enum ProfileState
    {
        None,
        Paused,
        Running,
        Stopped
    }

    internal static class NativeMethods
    {
        public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);


        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumWindowProc lpfn, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        public static string GetWindowText(IntPtr hWnd)
        {
            // Allocate correct string length first
            int length = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    public static class HBRelogApi
    {
        private static int HbProcID => HBRelogHelper.HbProcId;

        private static IRemotingApi HBRelogRemoteApi => HBRelogHelper.HBRelogRemoteApi;

        public static bool IsConnected => HBRelogHelper.IsConnected;

        public static string CurrentProfileName => HBRelogHelper.CurrentProfileName;

        public static void RestartWow() => HBRelogRemoteApi.RestartWow(HbProcID);

        public static void RestartHB() => HBRelogRemoteApi.RestartHB(HbProcID);

        public static string[] GetProfileNames() => HBRelogRemoteApi.GetProfileNames();

        public static void StartProfile(string profileName)
        {
            HBRelogRemoteApi.StartProfile(profileName);
        }

        public static void StopProfile(string profileName)
        {
            HBRelogRemoteApi.StopProfile(profileName);
        }

        public static void PauseProfile(string profileName)
        {
            HBRelogRemoteApi.PauseProfile(profileName);
        }

        public static void IdleProfile(string profileName, TimeSpan time)
        {
            HBRelogRemoteApi.IdleProfile(profileName, time);
        }

        public static void Logon(string character, string server, string customClass, string botBase, string profilePath)
        {
            HBRelogRemoteApi.Logon(HbProcID, character, server, customClass, botBase, profilePath);
        }

        public static int GetProfileStatus(string profileName)
        {
            return HBRelogRemoteApi.GetProfileStatus(profileName);
        }

        public static void SetProfileStatusText(string status)
        {
            HBRelogRemoteApi.SetProfileStatusText(HbProcID, status);
        }

        public static void ProfileLog(string msg)
        {
            HBRelogRemoteApi.ProfileLog(HbProcID, msg);
        }

        public static void SkipCurrentTask(string profileName)
        {
            HBRelogRemoteApi.SkipCurrentTask(profileName);
        }
    }

}