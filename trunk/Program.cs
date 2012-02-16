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
using System.Collections.Generic;
using System.Diagnostics;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Shell;
using System.Linq;
using System.Reflection;
using System.IO;

namespace HighVoltz.HBRelog
{
    public class Program
    {
        static Dictionary<string, string> CmdLineArgs = new Dictionary<string, string>();
        public static bool AutoStart { get; private set; }
        // delay in-between starting wow processes from same .exe
        public static int WowStartDelay { get; private set; }
        // delay in-between starting Honorbuddy processes from same .exe
        public static int HbStartDelay { get; private set; }
        [STAThread]
        public static void Main(params string[] args)
        {
            bool newInstance;
            using (Mutex m = new Mutex(true, "HBRelog", out newInstance))
            {
                if (newInstance)
                {
                    CmdLineArgs = ProcessCmdLineArgs(args);
                    AutoStart = CmdLineArgs.ContainsKey("AUTOSTART") ? true : false;
                    WowStartDelay = CmdLineArgs.ContainsKey("WOWDELAY") ?
                        GetCmdLineArgVal<int>(CmdLineArgs["WOWDELAY"]) : 0;
                    HbStartDelay = CmdLineArgs.ContainsKey("HBDELAY") ?
                        GetCmdLineArgVal<int>(CmdLineArgs["HBDELAY"]) : 0;
                    var app = new Application();
                    Window window = new MainWindow();
                    window.Show();
                    app.Run(window);
                }
                else
                {
                    Process currentProc = Process.GetCurrentProcess();
                    Process mutexOwner = Process.GetProcessesByName(currentProc.ProcessName).FirstOrDefault(p => p.Id != currentProc.Id);
                    if (mutexOwner != null)
                    {
                        NativeMethods.SetForegroundWindow(mutexOwner.MainWindowHandle);
                    }
                }
            }
        }

        static Dictionary<string, string> ProcessCmdLineArgs(string[] args)
        {
            Dictionary<string, string> cmdLineArgs = new Dictionary<string, string>();
            foreach (string s in args)
            {
                string[] tokens = s.Split('=');
                // make the / character optional
                string argName = (tokens[0][0] == '/' ? tokens[0].Substring(1) : tokens[0]).ToUpperInvariant();
                cmdLineArgs.Add(argName, tokens.Length > 1 ? tokens[1] : "");
            }
            return cmdLineArgs;
        }

        static T GetCmdLineArgVal<T>(string arg)
        {
            try
            {
                return (T)Convert.ChangeType(arg, typeof(T));
            }
            catch (Exception ex)
            {
                Log.Err("Unable to convert {0} to type: {1}\n{2}", arg, typeof(T), ex);
                return default(T);
            }
        }
    }
}
