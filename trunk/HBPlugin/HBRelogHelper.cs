//!CompilerOption:AddRef:Remoting.dll
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Serialization.Formatters;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using HighVoltz.HBRelog.Remoting;
using Styx;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Plugins.PluginClass;


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
        static ChannelFactory<IRemotingApi> pipeFactory;
        static internal HBRelogHelper Instance { get; private set; }

        public HBRelogHelper()
        {
            Instance = this;
            try
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                HbProcId = Process.GetCurrentProcess().Id;
                pipeFactory = new ChannelFactory<IRemotingApi>(new NetNamedPipeBinding(),
                        new EndpointAddress("net.pipe://localhost/HBRelog/Server"));

                HBRelogRemoteApi = pipeFactory.CreateChannel();

                //instead of spawning a new thread use the GUI one.
                Application.Current.Dispatcher.Invoke(new Action(
                    delegate
                    {
                        _monitorTimer = new DispatcherTimer();
                        _monitorTimer.Tick += MonitorTimerCB;
                        _monitorTimer.Interval = TimeSpan.FromSeconds(10);
                        _monitorTimer.Start();
                    }));
                IsConnected = HBRelogRemoteApi.Init(HbProcId);
                if (IsConnected)
                    CurrentProfileName = HBRelogRemoteApi.GetCurrentProfileName(HbProcId);
            }
            catch (Exception ex)
            {
                // fail silently.
                //Logging.Write(Color.Red, ex.ToString());
            }
            // since theres no point of this plugin showing up in plugin list lets just throw an exception.

            throw new Exception("Ignore this exception");
        }

        void Shutdown()
        {
            if (pipeFactory.State == CommunicationState.Opened || pipeFactory.State == CommunicationState.Opening)
            {
                pipeFactory.Close();
                pipeFactory.Abort();
            }
        }
        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Shutdown();
        }

        void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            Shutdown();
        }

        static string _lastStatus;
        static string _lastTooltip;
        static DateTime RunningTimeStamp = DateTime.Now;
        public static void MonitorTimerCB(object sender, EventArgs args)
        {
            if (!IsConnected)
                return;
            if (!TreeRoot.IsRunning)
            {
                int profileStatus = HBRelogRemoteApi.GetProfileStatus(CurrentProfileName);
                // if HB isn't running after 30 seconds 
                // and the HBRelog profile isn't paused then restart hb
                if (profileStatus != 1 && DateTime.Now - RunningTimeStamp >= TimeSpan.FromSeconds(50))
                    HBRelogRemoteApi.RestartHB(HbProcId);
            }
            else
                RunningTimeStamp = DateTime.Now;
            if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
            {
                HBRelogRemoteApi.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
                _lastStatus = TreeRoot.StatusText;
            }
            if (InfoPanel.IsMeasuring)
                UpdateTooltip();
        }

        private static void UpdateTooltip()
        {
            string tooltip = string.Empty;
            if (StyxWoW.Me.Level < 85)
                tooltip += string.Format("XP/hr: {0}\n", InfoPanel.XPPerHour);
            if (TreeRoot.Current.Name == "BGBuddy")
            {
                tooltip += string.Format("BGs: {0} ({1}/hr)\n",
                    InfoPanel.BGsCompleted, InfoPanel.BGsPerHour);
                tooltip += string.Format("BGs won: {0} ({1}/hr)\n",
                    InfoPanel.BGsWon, InfoPanel.BGsWonPerHour);
                tooltip += string.Format("BGs lost: {0} ({1}/hr)\n",
                    InfoPanel.BGsLost, InfoPanel.BGsLostPerHour);
                tooltip += string.Format("Honor/hr: {0}\n", InfoPanel.HonorPerHour);
            }
            else
            {
                tooltip += string.Format("Loots: {0} ({1}/hr)\n",
                    InfoPanel.Loots, InfoPanel.LootsPerHour);
                tooltip += string.Format("Deaths: {0} - ({1}/hr)\n",
                    InfoPanel.Deaths, InfoPanel.DeathsPerHour);
                if (TreeRoot.Current.Name != "Gatherbuddy2")
                {
                    tooltip += string.Format("Mobs killed: {0} - ({1}/hr)\n",
                    InfoPanel.MobsKilled, InfoPanel.MobsPerHour);
                }
            }
            if (tooltip != _lastTooltip)
            {
                HBRelogRemoteApi.SetBotInfoToolTip(HbProcId, tooltip);
                _lastTooltip = tooltip;
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

        public override bool WantButton { get { return true; } }
        public override void OnButtonPress()
        {
            Logging.Write("IsConnected: {0}", IsConnected);
            foreach (string name in HBRelogRemoteApi.GetProfileNames())
            {
                Logging.Write("GetProfileStatus: {0}", HBRelogRemoteApi.GetProfileStatus(name));
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
    }
}