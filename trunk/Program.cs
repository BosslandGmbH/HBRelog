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

namespace HighVoltz
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            bool newInstance;
            using (Mutex m = new Mutex(true, "HBRelog", out newInstance))
            {
                if (newInstance)
                {
                    var app = new Application();
                    /*
                    List<JumpItem> jumpListCollection = new List<JumpItem>()
                    {
                        new JumpTask(){ Title="Start", Description="Starts all enabled accounts", Arguments="/start", IconResourcePath=@"C:\Windows\System32\shell32.dll", IconResourceIndex=137},
                        new JumpTask(){ Title="Pause", Description="Pauses all enabled accounts", Arguments="/pause", IconResourcePath=@"C:\Windows\System32\mmcndmgr.dll", IconResourceIndex=34},
                        new JumpTask(){ Title="Stop", Description="Stops all enabled accounts", Arguments="/stop", IconResourcePath=@"C:\Windows\System32\wmploc.dll", IconResourceIndex=157},
                    };
                    JumpList jmpList = new JumpList(jumpListCollection, false, false);
                    JumpList.SetJumpList(app, jmpList);
                    */
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
    }
}
