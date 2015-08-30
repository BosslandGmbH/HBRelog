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
using Styx.Helpers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Xml;
using Styx.WoWInternals;

namespace HighVoltz.HBRelog.Remoting
{
    [ServiceContract(CallbackContract = typeof(IRemotingApiCallback),
        SessionMode = SessionMode.Required)]
    public interface IRemotingApi
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

        [OperationContract(IsOneWay = true)]
        void NotifyBotEvent(string what);
    }

    interface IRemotingApiCallback
    {
        [OperationContract]
        void StartBot(string botname, string profile);
        [OperationContract]
        void StopBot();
        [OperationContract]
        void ChangeProfile(string profileName);
    }

}

namespace HighVoltz.HBRelogHelper
{

    public class ServiceProxy : DuplexClientBase<IRemotingApi>, IRemotingApi
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

        public void NotifyBotEvent(string what)
        {
            Channel.NotifyBotEvent(what);
        }

        public void Heartbeat(int hbProcID)
        {
            Channel.Heartbeat(hbProcID);
        }


    }

    class CallbackHandler : IRemotingApiCallback
    {
        public void StartBot(string botname, string profile)
        {
            Logging.Write("HBRelog: starting bot");
            Util.QueueUserWorkItemOn(Application.Current.Dispatcher, () =>
            {
                if (TreeRoot.IsRunning || TreeRoot.IsPaused) return;
                // NB: If Honorbuddy main window renames the control or changes its type,
                // this application will need adjustment also...
                var controlName = "cmbBotSelector";
                var control = (ComboBox)Application.Current.MainWindow.FindName(controlName);
                if (control == null)
                {
                    var message = String.Format("Unable to locate \"{0}\" control", controlName);
                    Logging.Write(message);
                    throw new ArgumentException(message);
                }
                foreach (var i in control.Items)
                {
                    if (i.ToString().StartsWith(botname))
                    {
                        Logging.Write("changing botbase to " + control.SelectedItem);
                        control.SelectedItem = i;
                        break;
                    }
                }
                if (ProfileManager.CurrentProfile.Path != profile)
                {
                    ObjectManager.Update();
                    try
                    {
                        ProfileManager.LoadNew(profile);
                    }
                    catch (Exception e)
                    {
                        Logging.Write(e.ToString());
                    }
                }
                TreeRoot.Start();
            });
        }

        public void StopBot()
        {
            Util.QueueUserWorkItemOn(Application.Current.Dispatcher, () =>
            {
                if (!TreeRoot.IsRunning && !TreeRoot.IsPaused) return;
                TreeRoot.Stop("request from HBRelog");
            });
        }

        public void ChangeProfile(string profileName)
        {
            ProfileManager.LoadNew(profileName);
        }
    }

    public class HBRelogHelperSettings : Settings
    {

        public HBRelogHelperSettings()
            : base(Path.Combine(CharacterSettingsDirectory, "HBRelogHelper.xml"))
        {
            Load();
        }

        [Setting, DefaultValue("net.pipe://localhost/HBRelog/Server")]
        public string RemotingUri { get; set; }

    }
    public class HBRelogHelper : HBPlugin
    {
        static public bool IsConnected { get; private set; }
        static internal int HbProcId { get; private set; }
        static internal string CurrentProfileName { get; private set; }
        private static DispatcherTimer _monitorTimer;
        public static ServiceProxy Proxy;
        private static TreeRootState _lastTreeState;
        private static HBRelogHelperSettings MySettings;

        public HBRelogHelper()
        {
            try
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

                MySettings = new HBRelogHelperSettings();

                var hnd = new CallbackHandler();
                var ctx = new InstanceContext(hnd);
                Proxy = new ServiceProxy(ctx, new NetNamedPipeBinding(), new EndpointAddress(MySettings.RemotingUri));

                HbProcId = Process.GetCurrentProcess().Id;

                //instead of spawning a new thread use the GUI one.
                Application.Current.Dispatcher.Invoke(new Action(
                    delegate
                    {
                        _monitorTimer = new DispatcherTimer();
                        _monitorTimer.Tick += MonitorTimerCb;
                        _monitorTimer.Interval = TimeSpan.FromSeconds(2);
                        _monitorTimer.Start();
                    }));
                IsConnected = Proxy.Init(HbProcId);
	            if (IsConnected)
	            {
		            Logging.Write("HBRelogHelper: Connected with HBRelog");
		            CurrentProfileName = Proxy.GetCurrentProfileName(HbProcId);
	            }
	            else
	            {
					Logging.Write("HBRelogHelper: Could not connect to HBRelog");
	            }

                _lastTreeState = TreeRoot.State;

                TreeRoot.OnStatusTextChanged += (sender, args) =>
                {
                    // exit if TreeRoot.State did not changed
                    if (TreeRoot.State == _lastTreeState)
                        return;

                    switch (TreeRoot.State)
                    {
                        case TreeRootState.Stopped:
                        case TreeRootState.Stopping:
                            Proxy.NotifyBotStopped("");
                            break;
                        //case TreeRootState.Starting:
                        //case TreeRootState.Running:
                        //    Proxy.NotifyBotStarted();
                        //    break;
                    }
                    _lastTreeState = TreeRoot.State;
                };
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
                if (Proxy.ChannelFactory.State == CommunicationState.Opened ||
                    Proxy.State == CommunicationState.Opening)
                {
                    Proxy.ChannelFactory.Close();
                    Proxy.ChannelFactory.Abort();
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

                if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
                {
                    Proxy.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
                    _lastStatus = TreeRoot.StatusText;
                }

                if (HeartbeatTimer.IsFinished)
                {
                    Proxy.Heartbeat(HbProcId);
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
                    Proxy.SetBotInfoToolTip(HbProcId, tooltip);
                    _lastTooltip = tooltip;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
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
            foreach (string name in Proxy.GetProfileNames())
            {
                Logging.Write("{1}: GetProfileStatus: {0}", Proxy.GetProfileStatus(name), name);
                Proxy.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
            }
        }
    }

    public static class Util
    {
        // credits to Chinajade
        public static void InvokeOn(Dispatcher dispatcher, Action action)
        {
            if (!dispatcher.CheckAccess())
            {
                dispatcher.Invoke(DispatcherPriority.Normal, action);
                return;
            }

            action();
        }

        public static void QueueUserWorkItemOn(Dispatcher dispatcher, Action action)
        {
            ThreadPool.QueueUserWorkItem( _ =>
            {
                InvokeOn(dispatcher, action);
            });
        }
    }

    static public class HBRelogApi
    {
        private static int HbProcID
        { get { return HBRelogHelper.HbProcId; } }
        private static IRemotingApi HBRelogRemoteApi
        { get { return HBRelogHelper.Proxy; } }
        public static bool IsConnected { get { return HBRelogHelper.IsConnected; } }
        public static string CurrentProfileName { get { return HBRelogHelper.CurrentProfileName; } }

        public static void RestartWow()
        {
            HBRelogRemoteApi.RestartWow(HbProcID);
        }

        public static void RestartHB()
        {
            HBRelogRemoteApi.RestartHB(HbProcID);
        }

        public static string[] GetProfileNames()
        {
            return HBRelogRemoteApi.GetProfileNames();
        }

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

        public static void SkipCurrentTask(string profileName)
        {
            HBRelogRemoteApi.SkipCurrentTask(profileName);
        }
    }
}
