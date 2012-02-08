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