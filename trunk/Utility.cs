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
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Linq;

namespace HighVoltz.HBRelog
{
    static public class Utility
    {
        public const uint WmKeydown = 0x0100;
        public const uint WmChar = 0x0102;
        public const uint WmKeyup = 0x0101;
        public readonly static Random Rand = new Random();

        public static string EncodeToUTF8(this string text)
        {
            var buffer = new StringBuilder(Encoding.UTF8.GetByteCount(text)*2);
            byte[] utf8Encoded = Encoding.UTF8.GetBytes(text);
            foreach (byte b in utf8Encoded)
            {
                buffer.Append(string.Format("\\{0:D3}", b));
            }
            return buffer.ToString();
        }

        public static void UnblockFileIfZoneRestricted(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);
            string path = file + ":Zone.Identifier";
            if (NativeMethods.GetFileAttributes(path) != -1)
            {
                Log.Write("Removing Zone restrictions from {0}", file);
                NativeMethods.DeleteFile(path);
            }
        }
        public static bool HasInternetConnection
        {
            get
            {
                int state;
                return NativeMethods.InternetGetConnectedState(out state, 0);
            }
        }
        public static void ResizeAndMoveWindow(IntPtr hWnd, int x, int y, int width, int height)
        {
            NativeMethods.SetWindowPos(hWnd, new IntPtr(0), x, y, width, height,
                NativeMethods.SetWindowPosFlags.SWP_NOZORDER | NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE);
        }

        public static NativeMethods.Rect GetWindowRect(IntPtr hWnd)
        {
            var result = new NativeMethods.Rect();
            NativeMethods.GetWindowRect(hWnd, out result);
            return result;
        }

        public static bool Is64BitProcess(Process proc)
        {
            bool retVal;
            return Environment.Is64BitOperatingSystem &&
                !(NativeMethods.IsWow64Process(proc.Handle, out retVal) && retVal);
        }
        // returns base offset for main module
        static public uint BaseOffset(this Process proc)
        {
            return (uint)proc.MainModule.BaseAddress.ToInt32();
        }

        static public string VersionString(this Process proc)
        {
            return proc.MainModule.FileVersionInfo.FileVersion;
        }
    }
}
