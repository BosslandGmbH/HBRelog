/*
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
using System.IO;
using System.Linq;
using HighVoltz.HBRelog;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting;
using HighVoltz.HBRelog.Remoting;
using HighVoltz.HBRelog.Settings;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HighVoltz.HBRelog
{
    class HBRelogManager
    {
        public static GlobalSettings Settings { get; private set; }
        static public Thread WorkerThread { get; private set; }
        public static bool IsInitialized { get; private set; }
        private static DateTime _killWowErrsTimeStamp = DateTime.Now;

        static IpcChannel _ipcChannel;
        static HBRelogManager()
        {
            try
            {
                Settings = GlobalSettings.Load();
                WorkerThread = new Thread(DoWork) { IsBackground = true };
                WorkerThread.Start();

                var serverSinkProvider = new BinaryServerFormatterSinkProvider();
                serverSinkProvider.TypeFilterLevel = TypeFilterLevel.Full;

                IDictionary properties = new Hashtable();
                properties["portName"] = "HBRelogChannel";
                _ipcChannel = new IpcChannel(properties, null, serverSinkProvider);
                ChannelServices.RegisterChannel(_ipcChannel, true);

                RemotingConfiguration.RegisterWellKnownServiceType(
                    typeof(Remoting.RemotingApi),
                           "RemoteApi",
                           WellKnownObjectMode.Singleton);

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }
        static Regex _hbTitleRegex = new Regex(@"^\D*(?<id>\d+)\D*$");
        static void DoWork()
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

                        if (DateTime.Now - _killWowErrsTimeStamp >= TimeSpan.FromMinutes(1))
                        {
                            // check for wow error windows
                            foreach (var process in Process.GetProcessesByName("WowError"))
                            {
                                process.Kill();
                                Log.Write("Killing WowError process");
                            }
                            // check for stray HB instances that are not attached to a valid WOW process
                            foreach (var process in Process.GetProcessesByName("Honorbuddy"))
                            {
                                string title = NativeMethods.GetWindowText(process.MainWindowHandle);
                                var match = _hbTitleRegex.Match(title);
                                if (match.Success)
                                {
                                    int wowProcId = int.Parse(match.Groups["id"].Value);
                                    Process[] wowProcessIds = Process.GetProcessesByName("Wow");
                                    bool hbIsStray = !wowProcessIds.Any(proc => proc != null && !proc.HasExited && proc.Id == wowProcId);
                                    if (hbIsStray)
                                    {
                                        process.CloseMainWindow();
                                        Log.Write("Closing stray honorbuddy process.");
                                    }
                                }
                            }
                            _killWowErrsTimeStamp = DateTime.Now;
                        }

                    }
                }
                catch (Exception ex)
                {
                    Log.Err(ex.ToString());
                }
                finally
                {
                    // sleep for 1000 sec minus time it took to execute the fsm
                    int sleepTime = 1000 - (Environment.TickCount - pulseStartTime);
                    if (sleepTime > 0)
                        Thread.Sleep(sleepTime);
                }
            }
            // ReSharper disable FunctionNeverReturns
        }
        // ReSharper restore FunctionNeverReturns
    }
}
