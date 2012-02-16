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
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using HighVoltz.HBRelog.Remoting;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Plugins.PluginClass;


namespace HighVoltz.HBRelog.Remoting
{
    interface IRemotingApi
    {
        bool Init(int hbProcID);
        void RestartHB(int hbProcID);
        void RestartWow(int hbProcID);
        string[] GetProfileNames();
        string GetCurrentProfileName(int hbProcID);
        void StartProfile(string profileName);
        void StopProfile(string profileName);
        void PauseProfile(string profileName);
        void IdleProfile(string profileName, TimeSpan time);
        void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath);
        int GetProfileStatus(string profileName);
        void SetProfileStatusText(int hbProcID, string status);
    }
}

namespace HighVoltz.HBRelogHelper
{
    public class HBRelogHelper : HBPlugin
    {
        static public bool IsConnected { get; private set; }
        static internal IRemotingApi HBRelogRemoteApi;
        static internal int HbProcId;
        static DispatcherTimer _monitorTimer;
        static IpcChannel _ipcChannel;
        static internal HBRelogHelper Instance { get; private set; }

        public HBRelogHelper()
        {
            Instance = this;
            try
            {
                HbProcId = Process.GetCurrentProcess().Id;
                if (HBRelogRemoteApi == null)
                {
                    var serverSinkProvider = new BinaryServerFormatterSinkProvider();
                    serverSinkProvider.TypeFilterLevel = TypeFilterLevel.Full;

                    IDictionary properties = new Hashtable();
                    properties["portName"] = string.Format("HBRelogChannel_{0}", HbProcId);
                    _ipcChannel = new IpcChannel(properties, null, serverSinkProvider);
                    ChannelServices.RegisterChannel(_ipcChannel, true);
                    HBRelogRemoteApi =
                       (IRemotingApi)Activator.GetObject
                       (typeof(IRemotingApi),
                        "ipc://HBRelogChannel/RemoteApi");

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
                }
            }
            catch (Exception ex)
            {
                Logging.Write(Color.Red, ex.ToString());
            }
            // since theres no point of this plugin showing up in plugin list lets just throw an exception.
            throw new Exception("Ignore this exception");
        }

        static string _lastStatus;
        static DateTime RunningTimeStamp = DateTime.Now;
        public static void MonitorTimerCB(object sender, EventArgs args)
        {
            if (!TreeRoot.IsRunning)
            {
                // HB isn't running.. so lets restart it 
                if (DateTime.Now - RunningTimeStamp >= TimeSpan.FromSeconds(30))
                    HBRelogRemoteApi.RestartHB(HbProcId);
            }
            else
                RunningTimeStamp = DateTime.Now;
            if (TreeRoot.StatusText != _lastStatus && !string.IsNullOrEmpty(TreeRoot.StatusText))
            {
                HBRelogRemoteApi.SetProfileStatusText(HbProcId, TreeRoot.StatusText);
                _lastStatus = TreeRoot.StatusText;
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
    /*
     * 
       void RestartHB(int hbProcID);
        void RestartWoW(int hbProcID);
        string[] GetProfileNames();
        string GetCurrentProfileName(int hbProcID);
        void StartProfile(string profileName);
        void StopProfile(string profileName);
        void PauseProfile(string profileName);
        void IdleProfile(string profileName, TimeSpan time);
        void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath);
        int GetProfileStatus(string profileName);
        void SetProfileStatusText(int hbProcID, string status);
     */
    static public class HBRelogApi
    {
        static int HbProcID { get { return HBRelogHelper.HbProcId; } }
        static IRemotingApi HBRelogRemoteApi { get { return HBRelogHelper.HBRelogRemoteApi; } }
        public static bool IsConnected { get { return HBRelogHelper.IsConnected; } }
        public static void RestartWow() { HBRelogRemoteApi.RestartWow(HbProcID); }
        public static void RestartHB() { HBRelogRemoteApi.RestartHB(HbProcID); }
        public static string[] GetProfileeNames() { return HBRelogRemoteApi.GetProfileNames(); }
        public static void StartProfile(string profileName) { HBRelogRemoteApi.StartProfile(profileName); }
        public static void StopProfile(string profileName) { HBRelogRemoteApi.StopProfile(profileName); }
        public static void PauseProfile(string profileName) { HBRelogRemoteApi.PauseProfile(profileName); }
        public static void IdleProfile(string profileName, TimeSpan time)
        {
            HBRelogRemoteApi.IdleProfile(profileName, time);
        }

        #region Logon
        public static void Logon(string character)
        {
            Logon(character, null, null, null, null);
        }

        public static void Logon(string character, string server)
        {
            Logon(character, server, null, null, null);
        }

        public static void Logon(string character, string server, string customClass)
        {
            Logon(character, server, customClass, null, null);
        }

        public static void Logon(string character, string server, string customClass, string botBase)
        {
            Logon(character, server, customClass, botBase, null);
        }

        public static void Logon(string character, string server, string customClass, string botBase, string profilePath)
        {
            HBRelogRemoteApi.Logon(HbProcID, character, server, customClass, botBase, profilePath);
        }
        #endregion

        public static int GetProfileStatus(string profileName)
        {
            return HBRelogRemoteApi.GetProfileStatus(profileName);
        }

        public static void SetProfileStatusText(string status)
        {
            HBRelogRemoteApi.SetProfileStatusText(HbProcID,status);
        }
    }
}
