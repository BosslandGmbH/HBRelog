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
using Microsoft.Win32;
using HighVoltz.HBRelog.Settings;

namespace HighVoltz.HBRelog
{
    public class Program
    {
        private static Dictionary<string, string> s_cmdLineArgs = new Dictionary<string, string>();
        [STAThread]
        public static void Main(params string[] args)
        {
            var settingsPath = GlobalSettings.GetSettingsPath();
            var mutexName = Fnv1($"{GlobalSettings.GetSettingsPath()}|HBRelog|{MachineGuid}").ToString();
            using (Mutex m = new Mutex(true, mutexName, out bool newInstance))
            {
                if (newInstance)
                {
                    AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                    s_cmdLineArgs = ProcessCmdLineArgs(args);
                    if (s_cmdLineArgs.ContainsKey("AUTOSTART"))
                        HbRelogManager.Settings.AutoStart = true;
                    if (s_cmdLineArgs.ContainsKey("WOWDELAY"))
                        HbRelogManager.Settings.WowDelay = GetCmdLineArgVal<int>(s_cmdLineArgs["WOWDELAY"]);
                    if (s_cmdLineArgs.ContainsKey("HBDELAY"))
                        HbRelogManager.Settings.HBDelay = GetCmdLineArgVal<int>(s_cmdLineArgs["HBDELAY"]);

                    var app = new Application();
                    Window window = new MainWindow();
                    window.Show();
                    app.Run(window);
                }
                else
                {
                    // ToDO find the mutex owner and bring the process to foreground
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HbRelogManager.Shutdown();           
        }

        private static void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            HbRelogManager.Shutdown();
        }

        internal static bool GetCommandLineArgument<T>(string name, out T value)
        {
            value = default(T);
            string stringVal;
            if (!s_cmdLineArgs.TryGetValue(name.ToLowerInvariant(), out stringVal))
                return false;

            try
            {
                value = (T)Convert.ChangeType(stringVal, typeof(T));
            }
            catch (Exception ex)
            {
                Log.Err("Unable to convert {0} to type: {1}\n{2}", stringVal, typeof(T), ex);
                return false;
            }

            return true;
        }

        internal static bool HasCommandLineSwitch(string name)
        {
            return s_cmdLineArgs.ContainsKey(name.ToLowerInvariant());
        }

        private static Dictionary<string, string> ProcessCmdLineArgs(IEnumerable<string> args)
        {
            var cmdLineArgs = new Dictionary<string, string>();
            foreach (string s in args)
            {
                string[] tokens = s.Split('=',':');
                string argName = tokens[0].Replace("/", "").Replace("-", "").ToLowerInvariant();
                cmdLineArgs.Add(argName, tokens.Length > 1 ? tokens[1] : "");
            }
            return cmdLineArgs;
        }

        private static T GetCmdLineArgVal<T>(string arg)
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

        private static void LoadHBrelog()
        {
            var loaderName = GetLoaderName();
            var loaderExeName = loaderName + ".exe";
            if (!File.Exists(loaderName))
            {
                CreateLoader(loaderName);
            }
        }

        private static void CreateLoader(string loaderName)
        {

        }

        private static string GetLoaderName()
        {
            return Fnv1($"{MachineGuid}|HBRelog").ToString();
        }

        //   unique?

        private static string MachineGuid
        {
            get
            {
                //Check the HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\MachineGuid registry value.
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
                return (string)(key?.GetValue("MachineGuid"));
            }
        }

        private static int Fnv1(string s)
        {
            unchecked
            {
                uint hash = 0x811C9DC5;
                foreach (char c in s)
                {
                    hash *= 16777619;
                    hash ^= (byte)c;
                    hash *= 16777619;
                    hash ^= (byte)(c >> 8);
                }

                return (int)hash;
            }
        }
    }
}
