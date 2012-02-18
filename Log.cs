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
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Windows.Documents;

namespace HighVoltz.HBRelog
{
    public class Log
    {
        public static string ApplicationPath { get { return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName); } }
        static string _logPath;

        static Log()
        {
            string logFolder = Path.Combine(ApplicationPath, "Logs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
            _logPath = Path.Combine(logFolder, string.Format("Log[{0:yyyy-MM-dd_hh-mm-ss}].txt", DateTime.Now));
        }

        static public void Write(string format, params object[] args)
        {
            Write(Colors.Black, format, args);
        }

        static public void Err(string format, params object[] args)
        {
            Write(Colors.Red, format, args);
        }

        static public void Debug(string format, params object[] args)
        {
            Debug(Colors.Black, format, args);
        }

        static public void Write(Color color, string format, params object[] args)
        {
            if (MainWindow.Instance == null)
                return;
            if (Thread.CurrentThread == MainWindow.Instance.Dispatcher.Thread)
            {
                InternalWrite(color, string.Format(format, args));
                WriteToLog(format, args);
            }
            else
            {
                MainWindow.Instance.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        InternalWrite(color, string.Format(format, args));
                        WriteToLog(format, args);
                    }));
            }
        }

        static public void Write(Color hColor, string header, Color mColor, string format, params object[] args)
        {
            if (MainWindow.Instance == null)
                return;
            if (Thread.CurrentThread == MainWindow.Instance.Dispatcher.Thread)
            {
                InternalWrite(hColor, header, mColor, string.Format(format, args));
                WriteToLog(header + format, args);
            }
            else
            {
                MainWindow.Instance.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        InternalWrite(hColor, header, mColor, string.Format(format, args));
                        WriteToLog(header + format, args);
                    }));
            }
        }
        // same Write. might use a diferent tab someday.
        static public void Debug(Color color, string format, params object[] args)
        {
            if (MainWindow.Instance == null)
                return;
            if (Thread.CurrentThread == MainWindow.Instance.Dispatcher.Thread)
            {
                InternalWrite(color, string.Format(format, args));
                WriteToLog(format, args);
            }
            else
            {
                MainWindow.Instance.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        InternalWrite(color, string.Format(format, args));
                        WriteToLog(format, args);
                    }));
            }
        }

        static void InternalWrite(Color color, string text)
        {
            try
            {
                var rtb = MainWindow.Instance.LogTextBox;
                System.Windows.Media.Color msgColorMedia = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                var messageTR = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd);
                messageTR.Text = string.Format("[{0:T}] {1}\r", DateTime.Now, text);
                messageTR.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(msgColorMedia));
                rtb.ScrollToEnd();
            }
            catch { }
        }

        static void InternalWrite(Color headerColor, string header, Color msgColor, string format, params object[] args)
        {
            try
            {
                var rtb = MainWindow.Instance.LogTextBox;
                System.Windows.Media.Color headerColorMedia = System.Windows.Media.Color.FromArgb(headerColor.A, headerColor.R, headerColor.G, headerColor.B);
                System.Windows.Media.Color msgColorMedia = System.Windows.Media.Color.FromArgb(msgColor.A, msgColor.R, msgColor.G, msgColor.B);

                var headerTR = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd)
                {
                    Text = string.Format("[{0:T}] {1}", DateTime.Now, header)
                };
                headerTR.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(headerColorMedia));

                var messageTR = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd);
                string msg = String.Format(format, args);
                messageTR.Text = msg + '\r';
                messageTR.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(msgColorMedia));
                rtb.ScrollToEnd();
            }
            catch { }
        }

        public static void WriteToLog(string format, params object[] args)
        {
            try
            {
                using (StreamWriter logStringWriter = new StreamWriter(_logPath, true))
                {
                    if (logStringWriter != null)
                        logStringWriter.WriteLine(string.Format("[" + DateTime.Now.ToString() + "] " + format, args));
                }
            }
            catch { }
        }
    }
}
