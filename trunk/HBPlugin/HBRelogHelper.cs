//!CompilerOption:Optimize:On
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HighVoltz.HBRelog.Remoting;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Plugins;


namespace HighVoltz.HBRelog.Remoting
{
    [ServiceContract]
    interface IRemotingApi
    {
        [OperationContract]
        bool Init(int hbProcID);
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
        void SetBotInfoToolTip(int hbProcID, string tooltip);
        [OperationContract(IsOneWay = true)]
        void SkipCurrentTask(string profileName);
    }
}

namespace HighVoltz.HBRelogHelper
{
    public class HBRelogHelper : HBPlugin
    {
        static public bool IsConnected { get; private set; }
        static internal IRemotingApi HBRelogRemoteApi { get; private set; }
        static internal int HbProcId { get; private set; }
        static internal string CurrentProfileName { get; private set; }
        static DispatcherTimer _monitorTimer;
        //static IpcChannel _ipcChannel;
        static ChannelFactory<IRemotingApi> _pipeFactory;
        static internal HBRelogHelper Instance { get; private set; }

        public HBRelogHelper()
        {
            Instance = this;
            try
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

                HbProcId = Process.GetCurrentProcess().Id;
                _pipeFactory = new ChannelFactory<IRemotingApi>(new NetNamedPipeBinding(),
                        new EndpointAddress("net.pipe://localhost/HBRelog/Server"));

                HBRelogRemoteApi = _pipeFactory.CreateChannel();
                //instead of spawning a new thread use the GUI one.
                Application.Current.Dispatcher.Invoke(new Action(
                    delegate
                    {
                        _monitorTimer = new DispatcherTimer();
                        _monitorTimer.Tick += MonitorTimerCb;
                        _monitorTimer.Interval = TimeSpan.FromSeconds(10);
                        _monitorTimer.Start();
                    }));
                IsConnected = HBRelogRemoteApi.Init(HbProcId);
	            if (IsConnected)
	            {
		            Logging.Write("HBRelogHelper: Connected with HBRelog");
		            CurrentProfileName = HBRelogRemoteApi.GetCurrentProfileName(HbProcId);
	            }
	            else
	            {
					Logging.Write("HBRelogHelper: Could not connect to HBRelog");
	            }
            }
            catch (Exception ex)
            {
                // fail silently.
                Logging.Write(Colors.Red, ex.ToString());
            }
            // since theres no point of this plugin showing up in plugin list lets just throw an exception.
            // new HB doesn't catch exceptions 
            //  throw new Exception("Ignore this exception");
        }

        void Shutdown()
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
        void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Shutdown();
        }

        void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            Shutdown();
        }

        static string _lastStatus;
        static string _lastTooltip;
        public static void MonitorTimerCb(object sender, EventArgs args)
        {
            try
            {
                if (!IsConnected)
                    return;
                if (!StyxWoW.IsInGame)
                    return;

                if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
                {
                    HBRelogRemoteApi.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
                    _lastStatus = TreeRoot.StatusText;
                }
                if (GameStats.IsMeasuring)
                    UpdateTooltip();
            }
            catch (Exception ex)
            {
	            if (ex is CommunicationObjectFaultedException)
		            return;
				if (ex is EndpointNotFoundException)
					Logging.Write("Unable to connect to HBRelog");
                Logging.WriteException(ex);
            }
        }

        private static void UpdateTooltip()
        {
            try
            {
                string tooltip = string.Empty;
                if (StyxWoW.Me.Level < 90)
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

        public override string Author
        {
            get { return "HighVoltz"; }
        }

        public override string Name
        {
            get { return "HBRelogHelper"; }
        }

        public override void Pulse()
        {
        }

        public override Version Version
        {
            get { return new Version(1, 0); }
        }

        public override bool WantButton { get { return false; } }
        public override void OnButtonPress()
        {
            Logging.Write("IsConnected: {0}", IsConnected);
            foreach (string name in HBRelogRemoteApi.GetProfileNames())
            {
                Logging.Write("{1}: GetProfileStatus: {0}", HBRelogRemoteApi.GetProfileStatus(name), name);
                HBRelogRemoteApi.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
            }
        }
    }
    static public class HBRelogApi
    {
        static int HbProcID { get { return HBRelogHelper.HbProcId; } }
        static IRemotingApi HBRelogRemoteApi { get { return HBRelogHelper.HBRelogRemoteApi; } }
        public static bool IsConnected { get { return HBRelogHelper.IsConnected; } }
        public static string CurrentProfileName { get { return HBRelogHelper.CurrentProfileName; } }
        public static void RestartWow() { HBRelogRemoteApi.RestartWow(HbProcID); }
        public static void RestartHB() { HBRelogRemoteApi.RestartHB(HbProcID); }
        public static string[] GetProfileNames() { return HBRelogRemoteApi.GetProfileNames(); }
        public static void StartProfile(string profileName) { HBRelogRemoteApi.StartProfile(profileName); }
        public static void StopProfile(string profileName) { HBRelogRemoteApi.StopProfile(profileName); }
        public static void PauseProfile(string profileName) { HBRelogRemoteApi.PauseProfile(profileName); }
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

        public static void SkipCurrentTask(string profileName)
        {
            HBRelogRemoteApi.SkipCurrentTask(profileName);
        }
    }
}