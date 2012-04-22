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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace HighVoltz.HBRelog
{
    public class Log
    {
        private static readonly string LogPath;

        static Log()
        {
            string logFolder = Path.Combine(ApplicationPath, "Logs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
            LogPath = Path.Combine(logFolder, string.Format("Log[{0:yyyy-MM-dd_hh-mm-ss}].txt", DateTime.Now));
        }

        public static string ApplicationPath
        {
            get { return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName); }
        }

        public static void Write(string format, params object[] args)
        {
            Write(HbRelogManager.Settings.UseDarkStyle ? Colors.White : Colors.Black, format, args);
        }

        public static void Err(string format, params object[] args)
        {
            Write(Colors.Red, format, args);
        }

        public static void Debug(string format, params object[] args)
        {
            Debug(HbRelogManager.Settings.UseDarkStyle ? Colors.White : Colors.Black, format, args);
        }

        public static void Write(Color color, string format, params object[] args)
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

        public static void Write(Color hColor, string header, Color mColor, string format, params object[] args)
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
        public static void Debug(Color color, string format, params object[] args)
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

        private static void InternalWrite(Color color, string text)
        {
            try
            {
                RichTextBox rtb = MainWindow.Instance.LogTextBox;
                Color msgColorMedia = Color.FromArgb(color.A, color.R, color.G, color.B);
                var messageTr = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd)
                                    {Text = string.Format("[{0:T}] {1}\r", DateTime.Now, text)};
                messageTr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(msgColorMedia));
                rtb.ScrollToEnd();
            }
            catch
            {
            }
        }

        private static void InternalWrite(Color headerColor, string header, Color msgColor, string format,
                                          params object[] args)
        {
            try
            {
                RichTextBox rtb = MainWindow.Instance.LogTextBox;
                Color headerColorMedia = Color.FromArgb(headerColor.A, headerColor.R, headerColor.G, headerColor.B);
                Color msgColorMedia = Color.FromArgb(msgColor.A, msgColor.R, msgColor.G, msgColor.B);

                var headerTr = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd)
                                   {
                                       Text = string.Format("[{0:T}] {1}", DateTime.Now, header)
                                   };
                headerTr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(headerColorMedia));

                var messageTr = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd);
                string msg = String.Format(format, args);
                messageTr.Text = msg + '\r';
                messageTr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(msgColorMedia));
                rtb.ScrollToEnd();
            }
            catch
            {
            }
        }

        public static void WriteToLog(string format, params object[] args)
        {
            try
            {
                using (var logStringWriter = new StreamWriter(LogPath, true))
                {
                    logStringWriter.WriteLine(string.Format("[" + DateTime.Now.ToString(CultureInfo.InvariantCulture) + "] " + format, args));
                }
            }
            catch
            {
            }
        }
    }
}