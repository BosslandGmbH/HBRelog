//!CompilerOption:Optimize:On
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
using System.ServiceModel.Channels;
using Styx.CommonBot.Profiles;

namespace HighVoltz.HBRelog.Remoting
{
    [ServiceContract(CallbackContract = typeof(IRemotingApiCallback),
        SessionMode = SessionMode.Required)]
    interface IRemotingApi
    {
        [OperationContract]
        bool Init(int hbProcID);
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
        void SetBotInfoToolTip(int hbProcID, string tooltip);

        [OperationContract(IsOneWay = true)]
        void SkipCurrentTask(string profileName);

        [OperationContract(IsOneWay = true)]
        void NotifyBotStopped(string reason);
    }

    interface IRemotingApiCallback
    {
        [OperationContract]
        void StartBot();
        [OperationContract]
        void StopBot();
        [OperationContract]
        void ChangeProfile(string profileName);
    }

}

namespace HighVoltz.HBRelogHelper
{

    internal class ServiceProxy : DuplexClientBase<IRemotingApi>, IRemotingApi
    {
        public ServiceProxy(InstanceContext c, Binding b, EndpointAddress a)
            : base(c, b, a) { }

        static int HbProcID
        {
            get { return HBRelogHelper.HbProcId; }
        }

        public bool IsConnected
        {
            get { return HBRelogHelper.IsConnected; }
        }

        public string CurrentProfileName
        {
            get { return HBRelogHelper.CurrentProfileName; }
        }

        public void RestartWow()
        {
            Channel.RestartWow(HbProcID);
        }

        public void RestartHB()
        {
            Channel.RestartHB(HbProcID);
        }

        public bool Init(int hbProcID)
        {
            return Channel.Init(hbProcID);
        }

        public void RestartHB(int hbProcID)
        {
            Channel.RestartHB(hbProcID);
        }

        public void RestartWow(int hbProcID)
        {
            RestartWow(hbProcID);
        }

        public string[] GetProfileNames()
        {
            return Channel.GetProfileNames();
        }

        public string GetCurrentProfileName(int hbProcID)
        {
            return Channel.GetCurrentProfileName(hbProcID);
        }

        public void StartProfile(string profileName)
        {
            Channel.StartProfile(profileName);
        }

        public void StopProfile(string profileName)
        {
            Channel.StopProfile(profileName);
        }

        public void PauseProfile(string profileName)
        {
            Channel.PauseProfile(profileName);
        }
        public void IdleProfile(string profileName, TimeSpan time)
        {
            Channel.IdleProfile(profileName, time);
        }

        public void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath)
        {
            Channel.Logon(HbProcID, character, server, customClass, botBase, profilePath);
        }

        public void Logon(string character, string server, string customClass, string botBase, string profilePath)
        {
            Channel.Logon(HbProcID, character, server, customClass, botBase, profilePath);
        }

        public int GetProfileStatus(string profileName)
        {
            return Channel.GetProfileStatus(profileName);
        }

        public void SetProfileStatusText(int hbProcID, string status)
        {
            Channel.SetProfileStatusText(hbProcID, status);
        }

        public void SetBotInfoToolTip(int hbProcID, string tooltip)
        {
            Channel.SetBotInfoToolTip(hbProcID, tooltip);
        }

        public void SetProfileStatusText(string status)
        {
            Channel.SetProfileStatusText(HbProcID, status);
        }

        public void SkipCurrentTask(string profileName)
        {
            Channel.SkipCurrentTask(profileName);
        }

        public void NotifyBotStopped(string reason)
        {
            Channel.NotifyBotStopped(reason);
        }

        public void Heartbeat(int hbProcID)
        {
            Channel.Heartbeat(hbProcID);
        }


    }

    class CallbackHandler : IRemotingApiCallback
    {
        public void StartBot()
        {
            if (TreeRoot.State == TreeRootState.Stopped)
            {
                TreeRoot.Start();
            }
        }

        public void StopBot()
        {
            if (TreeRoot.State == TreeRootState.Running || TreeRoot.State == TreeRootState.Paused)
            {
                TreeRoot.Stop();
            }
        }

        public void ChangeProfile(string profileName)
        {
            ProfileManager.LoadNew(profileName);
        }
    }

    public class HBRelogHelper : HBPlugin
    {
        static public bool IsConnected { get; private set; }
        static internal int HbProcId { get; private set; }
        static internal string CurrentProfileName { get; private set; }
        private static DispatcherTimer _monitorTimer;
        private static ServiceProxy _proxy;
        private static TreeRootState _lastTreeState;

        public HBRelogHelper()
        {
            try
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

                var hnd = new CallbackHandler();
                var ctx = new InstanceContext(hnd);
                _proxy = new ServiceProxy(ctx, new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/HBRelog/Server"));

                HbProcId = Process.GetCurrentProcess().Id;

                //instead of spawning a new thread use the GUI one.
                Application.Current.Dispatcher.Invoke(new Action(
                    delegate
                    {
                        _monitorTimer = new DispatcherTimer();
                        _monitorTimer.Tick += MonitorTimerCb;
                        _monitorTimer.Interval = TimeSpan.FromSeconds(10);
                        _monitorTimer.Start();
                    }));
                IsConnected = _proxy.Init(HbProcId);
	            if (IsConnected)
	            {
		            Logging.Write("HBRelogHelper: Connected with HBRelog");
		            CurrentProfileName = _proxy.GetCurrentProfileName(HbProcId);
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

        private void Shutdown()
        {
            try
            {
                if (_proxy.ChannelFactory.State == CommunicationState.Opened ||
                    _proxy.State == CommunicationState.Opening)
                {
                    _proxy.ChannelFactory.Close();
                    _proxy.ChannelFactory.Abort();
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
                if (!IsConnected)
                    return;
                if (!StyxWoW.IsInGame)
                    return;

                if (TreeRoot.State == TreeRootState.Stopped && _lastTreeState != TreeRoot.State)
                {
                    _proxy.NotifyBotStopped("");
                    _lastTreeState = TreeRootState.Stopped;
                    Logging.Write("bot stopped, fire event");
                }
                if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
                {
                    _proxy.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
                    _lastStatus = TreeRoot.StatusText;
                }


                if (HeartbeatTimer.IsFinished)
                {
                    _proxy.Heartbeat(HbProcId);
                    HeartbeatTimer.Reset();
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
                    _proxy.SetBotInfoToolTip(HbProcId, tooltip);
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
            foreach (string name in _proxy.GetProfileNames())
            {
                Logging.Write("{1}: GetProfileStatus: {0}", _proxy.GetProfileStatus(name), name);
                _proxy.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
            }
        }
    }
}