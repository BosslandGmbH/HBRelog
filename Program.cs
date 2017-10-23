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
            var baseDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            // Run under 'System' user
            using (var identity = WindowsIdentity.GetCurrent())
            {
                if (!identity.IsSystem)
                {
                    var hbrelogArgs = string.Join(" ", args.Select(a => $"\"{a}\""));
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.Arguments = $"-accepteula -nobanner -i -s \"{Assembly.GetEntryAssembly().Location}\" {hbrelogArgs}";
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.FileName = Path.Combine(baseDirectory, "Tools", "PsExec.exe");
                    Process.Start(psi);
                    return;
                }
            }

            // Use a loader to hide assembly/process name
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                var loaderName = GetLoaderName();
                var cachePath = Path.Combine(baseDirectory, "Cache");
                if (!Directory.Exists(cachePath))
                    Directory.CreateDirectory(cachePath);

                var loaderPath = Path.Combine(cachePath, loaderName + ".exe");
                var launcherPath = Path.Combine(baseDirectory, "Tools", "Launcher.exe");
                if (!File.Exists(loaderPath)
                    || FileVersionInfo.GetVersionInfo(launcherPath).FileVersion != FileVersionInfo.GetVersionInfo(loaderPath).FileVersion)
                {
                    File.Copy(launcherPath, loaderPath, true);
                    // ToDO set permissions
                    EnsureLoaderPermissions(loaderPath);
                }

                ProcessStartInfo psi = new ProcessStartInfo(loaderPath, string.Join(" ", args.Select(a => $"\"{a}\"")));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = false;
                psi.WorkingDirectory = baseDirectory;
                using (var proc = Process.Start(psi))
                {
                    proc.StandardInput.WriteLine("HBRelog");
                    proc.StandardInput.WriteLine(Assembly.GetExecutingAssembly().Location);
                }
                return;
            }

            var settingsPath = GlobalSettings.GetSettingsPath();
            var mutexName = Fnv1($"{GlobalSettings.GetSettingsPath()}|HBRelog|{MachineGuid}").ToString();
            using (Mutex m = new Mutex(true, mutexName, out bool newInstance))
            {
                if (!newInstance)
                {
                    // ToDO find the mutex owner and bring the process to foreground
                    return;
                }


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
                window.Activate();
                app.Run(window);
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName askedAssembly = new AssemblyName(args.Name);
            string[] fields = args.Name.Split(',');
            string name = fields[0];
            string culture = fields[2];
            // failing to ignore queries for satellite resource assemblies 
            // or using [assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)] 
            // in AssemblyInfo.cs will crash the program on non en-US based system cultures.
            if (name.EndsWith(".resources") && !culture.EndsWith("neutral"))
                return null;

            var path = Path.Combine(Utility.AssemblyDirectory, "Tools", name + ".exe");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

            path = Path.Combine(Utility.AssemblyDirectory, "Tools", name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

            path = Path.Combine(Utility.AssemblyDirectory, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

            return null;
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

        public static void EnsureLoaderPermissions(string path)
        {
            var fs = new FileSecurity();
            fs.SetAccessRuleProtection(true, false);
            fs.AddAccessRule(
                new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().Name,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

            fs.AddAccessRule(
                new FileSystemAccessRule(
                    "BUILTIN\\Administrators",
                    FileSystemRights.Delete,
                    AccessControlType.Allow));

            File.SetAccessControl(path, fs);
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
