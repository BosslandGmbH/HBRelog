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
using System.CodeDom.Compiler;
using System.Security.Principal;
using System.Security.AccessControl;

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
                var stage1Loading = AppDomain.CurrentDomain.IsDefaultAppDomain();
                if (stage1Loading)
                {
                    if (!newInstance)
                    {
                        // ToDO find the mutex owner and bring the process to foreground
                        return;
                    }
                    LoadHBrelog(args);
                    return;
                }

                var loaderPath = Process.GetCurrentProcess().MainModule.FileName;
                EnsureLoaderPermissions(loaderPath);
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

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
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.GetExecutingAssembly();
            throw new NotImplementedException();
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

        private static void LoadHBrelog(params string[] args)
        {
            var baseDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var loaderName = GetLoaderName();
            var cachePath = Path.Combine(baseDirectory, "Cache");
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);

            var loaderPath = Path.Combine(cachePath, loaderName + ".exe");
            if (!File.Exists(loaderPath) && !CreateLoader(loaderPath))
                return;

            ProcessStartInfo psi = new ProcessStartInfo();
            var hbrelogArgs = args.Any() ? '"' + string.Join("\" \"", args) + '"' : "";

            psi.Arguments = $"-accepteula -nobanner -i -s \"{loaderPath}\" {hbrelogArgs}";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.FileName = Path.Combine(baseDirectory, "Tools", "PsExec.exe");
            Process.Start(psi);
        }

        private static bool CreateLoader(string loaderPath)
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var hbRelogPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "..", "HBRelog.exe");
            var src = @"
using System;
using System.IO;
using System.Windows;
using System.Reflection;
namespace __Loader__
{
    public class Program
    {
        [STAThread]
        public static int Main(params string[] args)
        {
            var hbRelogInstallPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "".."");
            var hbRelogPath = Path.Combine(hbRelogInstallPath, ""HBRelog.exe"");
            var setup = new AppDomainSetup
            {
                ApplicationBase = hbRelogInstallPath,
            };

            return AppDomain.CreateDomain(""Domain"", null, setup).ExecuteAssembly(hbRelogPath, args);
        }
    }
}
";

            using (CodeDomProvider cc = CodeDomProvider.CreateProvider("CSharp"))
            {
                CompilerParameters cp = new CompilerParameters();
                cp.GenerateInMemory = false;
                cp.GenerateExecutable = true;
                cp.OutputAssembly = loaderPath;
                cp.IncludeDebugInformation = false;
                cp.CompilerOptions = "/optimize /platform:x86 /target:winexe";

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    cp.ReferencedAssemblies.Add(asm.Location);
                }

                var results = cc.CompileAssemblyFromSource(cp, src);
                if (results.Errors.HasErrors)
                {
                    foreach (var err in results.Errors.OfType<CompilerError>())
                        Log.Err($"{err}");
                    return false;
                }
                return true;
            } 
        }

        public static void EnsureLoaderPermissions(string path)
        {
            var accessControl = File.GetAccessControl(path, AccessControlSections.Owner);
            string user = accessControl.GetOwner(typeof(NTAccount)).ToString();
            if (user != "NT AUTHORITY\\SYSTEM")
            {
                var ntAccount = new NTAccount("NT AUTHORITY\\SYSTEM");
                accessControl.SetOwner(ntAccount);
                File.SetAccessControl(path, accessControl);
            }
        }

        static string GetLoaderName()
        {
            return Fnv1($"{MachineGuid}|HBRelog|{GlobalSettings.GetSettingsPath()}").ToString();
        }

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
