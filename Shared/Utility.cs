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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Shared
{
    public static class Utility
    {
        public const uint WmKeydown = 0x0100;
        public const uint WmChar = 0x0102;
        public const uint WmKeyup = 0x0101;
        public static readonly Random Rand = new Random();
        public static readonly string AssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static bool IsUserAdministrator()
        {
            //bool value to hold our return value
            bool isAdmin;
            try
            {
                //get the currently logged in user
                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException)
            {
                isAdmin = false;
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            return isAdmin;
        }

        public static bool HasInternetConnection
        {
            get
            {
                int state;
                return NativeMethods.InternetGetConnectedState(out state, 0);
            }
        }

        public static string EncodeToUTF8(this string text)
        {
            var buffer = new StringBuilder(Encoding.UTF8.GetByteCount(text)*2);
            var utf8Encoded = Encoding.UTF8.GetBytes(text);
            foreach (var b in utf8Encoded)
            {
                buffer.Append(string.Format("\\{0:D3}", b));
            }
            return buffer.ToString();
        }

        public static void UnblockFileIfZoneRestricted(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);
            var path = file + ":Zone.Identifier";
            if (NativeMethods.GetFileAttributes(path) != -1)
            {
                //Log.Write("Removing Zone restrictions from {0}", file);
                NativeMethods.DeleteFile(path);
            }
        }

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(HandleRef hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(HandleRef hWnd, StringBuilder lpString, int nMaxCount);

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

        public static NativeMethods.WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            var wi = new NativeMethods.WindowInfo(true);
            NativeMethods.GetWindowInfo(hWnd, ref wi);
            return wi;
        }

        public static Process GetChildProcessByName(int parentPid, string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.FirstOrDefault(process => IsChildProcessOf(parentPid, process));
        }

        public static bool IsChildProcessOf(int parentPid, Process child)
        {
            var childPid = child.Id;
            try
            {
                while (true)
                {
                    var childParrentPid = NativeMethods.ParentProcessUtilities.GetParentProcessId(childPid);
                    if (childParrentPid <= 0) return false;
                    if (childParrentPid == parentPid) return true;
                    childPid = childParrentPid;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // returns base offset for main module
        public static uint BaseOffset(this Process proc)
        {
            return (uint) proc.MainModule.BaseAddress.ToInt32();
        }

        public static string VersionString(this Process proc)
        {
            return proc.MainModule.FileVersionInfo.FileVersion;
        }

        /// <summary>
        ///     Encrpts the string using dpapi and returns a base64 string of encrypted data
        /// </summary>
        /// <param name="clearData">The clear data.</param>
        /// <returns></returns>
        public static string EncrptDpapi(string clearData)
        {
            var data = Encoding.Unicode.GetBytes(clearData);
            data = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        /// <summary>
        ///     Decrypts the base64 string using dpapi and returns the unencrpyted text.
        /// </summary>
        /// <param name="base64Data">The clear data.</param>
        /// <returns></returns>
        public static string DecrptDpapi(string base64Data)
        {
            var data = Convert.FromBase64String(base64Data);
            data = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.Unicode.GetString(data);
        }

        public static string DecryptAes(string clearText, byte[] key, byte[] iv)
        {
            var cipherBytes = Convert.FromBase64String(clearText);
            using (var algorithm = Aes.Create())
            {
                //  algorithm.Padding = PaddingMode.None;
                using (var decryptor = algorithm.CreateDecryptor(key, iv))
                {
                    using (var m = new MemoryStream())
                    {
                        using (var c = new CryptoStream(m, decryptor, CryptoStreamMode.Write))
                        {
                            c.Write(cipherBytes, 0, cipherBytes.Length);
                            c.FlushFinalBlock();
                        }
                        var a = m.ToArray();
                        return Encoding.Unicode.GetString(a);
                    }
                }
            }
        }

        public static string EncryptAes(string clearText, byte[] key, byte[] iv)
        {
            var cipherBytes = Encoding.Unicode.GetBytes(clearText);

            using (var algorithm = Aes.Create())
            {
                // algorithm.Padding = PaddingMode.None;
                using (var decryptor = algorithm.CreateEncryptor(key, iv))
                {
                    using (var m = new MemoryStream())
                    {
                        using (var c = new CryptoStream(m, decryptor, CryptoStreamMode.Write))
                        {
                            c.Write(cipherBytes, 0, cipherBytes.Length);
                            c.FlushFinalBlock();
                        }
                        var a = m.ToArray();
                        return Convert.ToBase64String(a);
                    }
                }
            }
        }

        #region Key and Mouse sending utility functions.

        public static bool SendBackgroundKey(IntPtr hWnd, char key, bool useVmChar = true)
        {
            var scanCode = NativeMethods.MapVirtualKey(key, 0);
            var lParam = (UIntPtr) (0x00000001 | (scanCode << 16));
            if (useVmChar)
                return SendMessage(hWnd, NativeMethods.Message.VM_CHAR, key, lParam);
            return SendMessage(hWnd, NativeMethods.Message.KEY_DOWN, key, lParam) &&
                   SendMessage(hWnd, NativeMethods.Message.KEY_UP, key, lParam);
        }

        public static bool SendBackgroundKeyCombination(IntPtr hWnd, char key, params char[] modifiers)
        {
            var ret = true;
            foreach (var mod in modifiers)
            {
                var scanCode = NativeMethods.MapVirtualKey(mod, 0);
                var lParam = (UIntPtr)(0x00000001 | (scanCode << 16));
                ret &= SendMessage(hWnd, NativeMethods.Message.KEY_DOWN, mod, lParam);
            }
            {
                var scanCode = NativeMethods.MapVirtualKey(key, 0);
                var lParam = (UIntPtr) (0x00000001 | (scanCode << 16));
                ret &= SendMessage(hWnd, NativeMethods.Message.KEY_DOWN, key, lParam) &&
                       SendMessage(hWnd, NativeMethods.Message.KEY_UP, key, lParam);
            }
            foreach (var mod in modifiers.Reverse())
            {
                var scanCode = NativeMethods.MapVirtualKey(mod, 0);
                var lParam = (UIntPtr)(0x00000001 | (scanCode << 16));
                ret &= SendMessage(hWnd, NativeMethods.Message.KEY_UP, mod, lParam);
            }
            return ret;
        }

        public static void SendBackgroundString(IntPtr hWnd, string str, bool downUp = true)
        {
            foreach (var chr in str)
            {
                SendBackgroundKey(hWnd, chr, downUp);
            }
        }

        public static void PasteBackgroundString(IntPtr hWnd, string str)
        {
            NativeMethods.OpenClipboard(IntPtr.Zero);
            var ptr = Marshal.StringToHGlobalUni(str);
            NativeMethods.SetClipboardData(13, ptr);
            NativeMethods.CloseClipboard();
            Marshal.FreeHGlobal(ptr);
            SendBackgroundKeyCombination(hWnd, (char)Keys.V, (char)Keys.ControlKey);
        }

        public static void PostBackgroundKey(IntPtr hWnd, char key, bool useVmChar = true)
        {
            var scanCode = NativeMethods.MapVirtualKey(key, 0);
            var lParam = (UIntPtr) (0x00000001 | (scanCode << 16));
            if (useVmChar)
            {
                PostMessage(hWnd, NativeMethods.Message.VM_CHAR, key, lParam);
            }
            else
            {
                PostMessage(hWnd, NativeMethods.Message.KEY_DOWN, key, lParam);
                PostMessage(hWnd, NativeMethods.Message.VM_CHAR, key, lParam);
                PostMessage(hWnd, NativeMethods.Message.KEY_UP, key, lParam);
            }
        }

        public static void PostBackgroundString(IntPtr hWnd, string str, bool downUp = true)
        {
            foreach (var chr in str)
            {
                PostBackgroundKey(hWnd, chr);
            }
        }

        private static bool SendMessage(IntPtr hWnd, NativeMethods.Message msg, char key, UIntPtr lParam)
        {
            for (var cnt = 0; cnt < 4; cnt++)
            {
                if (NativeMethods.SendMessage(hWnd, (uint) msg, (IntPtr) key, lParam) != IntPtr.Zero)
                    continue;
                return true;
            }
            return false;
        }

        private static void PostMessage(IntPtr hWnd, NativeMethods.Message msg, char key, UIntPtr lParam)
        {
            NativeMethods.PostMessage(hWnd, (uint) msg, (IntPtr) key, lParam);
        }

        private const int SizeOfInput = 28;

        public static async Task<bool> LeftClickAtPosAsync(
            IntPtr hWnd, int x, int y, bool doubleClick = false, bool restore = true, Func<bool> restoreCondition = null)
        {
            LeftClickAtPos(hWnd, x, y, doubleClick, restore, restoreCondition);
            await Task.Delay(10);
            return true;
        }

        public static void LeftClickAtPos(
            IntPtr hWnd, int x, int y, bool doubleClick = false, bool restore = true, Func<bool> restoreCondition = null)
        {
            var wndBounds = GetWindowRect(hWnd);
            double fScreenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CXSCREEN) - 1;
            double fScreenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CYSCREEN) - 1;
            var fx = (wndBounds.Left + x)*(65535.0f/fScreenWidth);
            var fy = (wndBounds.Top + y)*(65535.0f/fScreenHeight);

            var structInput = new NativeMethods.Input {type = NativeMethods.SendInputEventType.InputMouse};
            structInput.mkhi.mi.dwFlags = NativeMethods.MouseEventFlags.ABSOLUTE | NativeMethods.MouseEventFlags.MOVE |
                                          NativeMethods.MouseEventFlags.LEFTDOWN |
                                          NativeMethods.MouseEventFlags.LEFTUP;
            structInput.mkhi.mi.dx = (int) fx;
            structInput.mkhi.mi.dy = (int) fy;

            var forefroundWindow = NativeMethods.GetForegroundWindow();

            if (restore)
                SaveForegroundWindowAndMouse();
            try
            {
                NativeMethods.BlockInput(true);

                for (var num = 0; forefroundWindow != hWnd && num < 1000; num++)
                {
                    NativeMethods.SetForegroundWindow(hWnd);
                    Thread.Sleep(10);
                    forefroundWindow = NativeMethods.GetForegroundWindow();
                }

                NativeMethods.SendInput(1, ref structInput, SizeOfInput);
                Thread.Sleep(100);
                if (doubleClick)
                {
                    NativeMethods.SendInput(1, ref structInput, SizeOfInput);
                    Thread.Sleep(100);
                }
            }
            finally
            {
                if (restore)
                {
                    try
                    {
                        if (restoreCondition != null)
                        {
                            while (!restoreCondition())
                                Thread.Sleep(1);
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                        RestoreForegroundWindowAndMouse();
                    }
                    catch
                    {
                    }
                }
                NativeMethods.BlockInput(false);
            }
        }

        /// <summary>
        ///     Sleeps until condition becomes true or after timeout has been reached.
        /// </summary>
        /// <param name="condition">The until condition.</param>
        /// <param name="maxSleepTime">The max sleep time.</param>
        /// <returns>returns true if condition returned true before reaching timeout</returns>
        public static bool SleepUntil(Func<bool> condition, TimeSpan maxSleepTime)
        {
            var sleepStart = DateTime.Now;
            var timeOut = false;
            while (!condition() && (timeOut = DateTime.Now - sleepStart >= maxSleepTime) == false)
                Thread.Sleep(10);
            return !timeOut;
        }

        public static async Task<bool> WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout, int pollDelayMilliseconds = 10)
        {
            // TODO implement adaptive waiting
            // so predicate does not being polled too often
            // if we far from timeout margin we can increase polling delay up to exp(x)
            var t = Stopwatch.StartNew();
            var isNotTimeout = true;
            var ms = pollDelayMilliseconds;
            while (!await predicate() && isNotTimeout)
            {
                isNotTimeout = t.ElapsedMilliseconds < timeout.TotalMilliseconds;
                await Task.Delay(ms);
            }
            return isNotTimeout;
        }

        public static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, int pollDelayMilliseconds = 10)
        {
            // TODO implement adaptive waiting
            // so predicate does not being polled too often
            // if we far from timeout margin we can increase polling delay up to exp(x)
            var t = Stopwatch.StartNew();
            var isNotTimeout = true;
            var ms = pollDelayMilliseconds;
            while (!predicate() && isNotTimeout)
            {
                isNotTimeout = t.ElapsedMilliseconds < timeout.TotalMilliseconds;
                await Task.Delay(ms);
            }
            return isNotTimeout;
        }
        public static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMilliseconds = 1000, int pollDelayMilliseconds = 10)
        {
            return
                await WaitUntilAsync(predicate, TimeSpan.FromMilliseconds(timeoutMilliseconds), pollDelayMilliseconds);
        }

        public static async Task<bool> WaitUntilAsync(Func<Task<bool>> predicate, int timeoutMilliseconds = 1000, int pollDelayMilliseconds = 10)
        {
            return
                await WaitUntilAsync(predicate, TimeSpan.FromMilliseconds(timeoutMilliseconds), pollDelayMilliseconds);
        }

        private static IntPtr _originalForegroundWindow;
        private static Point _originalMousePos;

        public static void SaveForegroundWindowAndMouse()
        {
            _originalForegroundWindow = NativeMethods.GetForegroundWindow();
            NativeMethods.GetCursorPos(out _originalMousePos);
        }

        public static void RestoreForegroundWindowAndMouse()
        {
            var forefroundWindow = NativeMethods.GetForegroundWindow();
            for (var num = 0; forefroundWindow != _originalForegroundWindow && num < 1000; num++)
            {
                if (!NativeMethods.SetForegroundWindow(_originalForegroundWindow))
                    break;
                Thread.Sleep(1);
                forefroundWindow = NativeMethods.GetForegroundWindow();
            }

            var structInput = new NativeMethods.Input {type = NativeMethods.SendInputEventType.InputMouse};
            double fScreenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CXSCREEN) - 1;
            double fScreenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CYSCREEN) - 1;

            structInput.mkhi.mi.dwFlags = NativeMethods.MouseEventFlags.ABSOLUTE | NativeMethods.MouseEventFlags.MOVE;
            structInput.mkhi.mi.dx = (int) (_originalMousePos.X*(65535.0f/fScreenWidth));
            structInput.mkhi.mi.dy = (int) (_originalMousePos.Y*(65535.0f/fScreenHeight));
            NativeMethods.SendInput(1, ref structInput, SizeOfInput);
        }

        #endregion
    }
}