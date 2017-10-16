﻿/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Diagnostics;
using System.Threading;
using HighVoltz.HBRelog.Remoting;
using HighVoltz.HBRelog.Settings;
using System.ServiceModel;
using System.Windows;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HighVoltz.HBRelog
{
    internal class HbRelogManager
    {
        public static GlobalSettings Settings => GlobalSettings.Instance;
	    static public Thread WorkerThread { get; private set; }
        public static bool IsInitialized { get; private set; }
        private static Stopwatch _crashCheckTimer = Stopwatch.StartNew();
        private static Stopwatch _updateRealmStatusTimer = Stopwatch.StartNew();
        static readonly ServiceHost _host;
        public static WowRealmStatus WowRealmStatus { get; private set; }

        static HbRelogManager()
        {
            try
            {
                // if in designer mode then return
                if (MainWindow.Instance == null || DesignerProperties.GetIsInDesignMode(MainWindow.Instance))
                    return; 
                WorkerThread = new Thread(DoWork) { IsBackground = true };
                WorkerThread.Start();
                try
                {
                    _host = new ServiceHost(typeof(RemotingApi), new Uri("net.pipe://localhost/HBRelog"));
                    _host.AddServiceEndpoint(typeof(IRemotingApi),
                        new NetNamedPipeBinding() { ReceiveTimeout = TimeSpan.MaxValue },
                        "Server");
                    _host.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    Log.Err(ex.ToString());
                }

                WowRealmStatus = new WowRealmStatus();
                // update Wow Realm status
                if (Settings.CheckRealmStatus)
                    WowRealmStatus.Update();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Log.Err(ex.ToString());
            }
        }

        internal static void DoWork()
        {
            int pulseStartTime = 0;
            while (true)
            {
                try
                {
                    pulseStartTime = Environment.TickCount;
                    if (Utility.HasInternetConnection)
                    {
                        foreach (var character in Settings.CharacterProfiles)
                        {
                            if (character.IsRunning)
                                character.Pulse();
                        }

                        if (_crashCheckTimer.ElapsedMilliseconds >= 5000)
                        {
                            KillWoWCrashDialogs();
                            KillHonorbuddyCrashDialogs();
                            _crashCheckTimer.Restart();
                        }

                        if (Settings.CheckRealmStatus)
                        {
                            if (_updateRealmStatusTimer == null)
                                _updateRealmStatusTimer = Stopwatch.StartNew();

                            if (_updateRealmStatusTimer.ElapsedMilliseconds >= 60000)
                            {
                                // update Wow Realm status
                                if (Settings.CheckRealmStatus)
                                    WowRealmStatus.Update();
                                _updateRealmStatusTimer.Restart();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Err(ex.ToString());
                }
                finally
                {
                    // sleep for 1000 millisec minus time it took to execute
                    int sleepTime = 1000 - (Environment.TickCount - pulseStartTime);
                    if (sleepTime > 0)
                        Thread.Sleep(sleepTime);
                }
            }

        }

        public static void Shutdown()
        {
            if (_host.State == CommunicationState.Opened || _host.State == CommunicationState.Opening)
            {
                _host.Close();
                _host.Abort();
            }
        }


        private static void KillHonorbuddyCrashDialogs()
        {
            var processes =
                Process.GetProcessesByName("WerFault")
                .Where(p => p.MainWindowTitle == "Honorbuddy")
                .ToList();

            // check for wow error windows
            foreach (var process in processes)
            {
                process.Kill();
                Log.Write("Killing crashed Honorbuddy process");
            }
        }

        private static void KillWoWCrashDialogs()
        {
            var processes = Process.GetProcessesByName("BlizzardError")
                .Concat(Process.GetProcessesByName("WerFault")
                .Where(p => p.MainWindowTitle == "World of Warcraft"));

            // check for wow error windows
            foreach (var process in processes)
            {
                process.Kill();
                Log.Write("Killing crashed WoW process");
                process.Dispose();
            }
        }
    }
}
