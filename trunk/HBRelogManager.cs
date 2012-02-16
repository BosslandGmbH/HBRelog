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
using System.IO;

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
                _ipcChannel =  new IpcChannel(properties, null, serverSinkProvider);
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
                        // check for wow error windows
                        if (DateTime.Now - _killWowErrsTimeStamp >= TimeSpan.FromMinutes(1))
                        {
                            foreach (var process in Process.GetProcessesByName("WowError"))
                            {
                                process.Kill();
                                Log.Write("Killing WowError process");
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
