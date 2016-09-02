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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HighVoltz.HBRelog
{
    internal static class Utility
    {
        public const uint WmKeydown = 0x0100;
        public const uint WmChar = 0x0102;
        public const uint WmKeyup = 0x0101;
        public static readonly Random Rand = new Random();
        public static readonly string AssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static bool HasInternetConnection
        {
            get
            {
                int state;
                return NativeMethods.InternetGetConnectedState(out state, 0);
            }
        }

        public static void UnblockFileIfZoneRestricted(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);
            var path = file + ":Zone.Identifier";
            if (NativeMethods.GetFileAttributes(path) != -1)
            {
                Log.Write("Removing Zone restrictions from {0}", file);
                NativeMethods.DeleteFile(path);
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

        public static NativeMethods.WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            var wi = new NativeMethods.WindowInfo(true);
            NativeMethods.GetWindowInfo(hWnd, ref wi);
            return wi;
        }

        public static Process GetChildProcessByName(int parentPid, string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            var result =  processes.FirstOrDefault(process => IsChildProcessOf(parentPid, process));

	        if (result != null)
		        return result;

			// search for a process whose exe was renamed. 
	        return (from proc in Process.GetProcesses()
		        let procPath = GetProcessPath(proc)
		        where !string.IsNullOrEmpty(procPath) && File.Exists(procPath)
		        let exeOriginalNameWithExtention = FileVersionInfo.GetVersionInfo(procPath).OriginalFilename
		        where !string.IsNullOrEmpty(exeOriginalNameWithExtention)
				let exeOriginalName = Path.GetFileNameWithoutExtension(exeOriginalNameWithExtention)
				where exeOriginalName != null && exeOriginalName.Equals(processName, StringComparison.OrdinalIgnoreCase) && IsChildProcessOf(parentPid, proc)
					select proc).FirstOrDefault();
        }

	    private static string GetProcessPath(Process proc)
	    {
			// Wrapped in a try/catch since some processes, such as those that are started in suspend state, 
			// will throw exceptions when MainModule is accessed.
		    try
		    {
			    return proc.MainModule.FileName;
		    }
		    catch (Exception)
		    {
				return null;
		    }   
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

        public static void SendBackgroundString(IntPtr hWnd, string str, bool downUp = true)
        {
            foreach (var chr in str)
            {
                SendBackgroundKey(hWnd, chr, downUp);
            }
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

        public static void LeftClickAtPos(
            IntPtr hWnd, int x, int y, bool doubleClick = false, bool restore = true, Func<bool> restoreCondition = null)
        {
            var wndBounds = GetWindowRect(hWnd);
            double fScreenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CXSCREEN) - 1;
            double fScreenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetric.SM_CYSCREEN) - 1;
            var fx = (wndBounds.Left + x)*(65535.0f/fScreenWidth);
            var fy = (wndBounds.Top + y)*(65535.0f/fScreenHeight);

            var structInput = new NativeMethods.Input {type = NativeMethods.SendInputEventType.InputMouse};
            structInput.mkhi.mi.dwFlags = NativeMethods.MouseEventFlags.ABSOLUTE | NativeMethods.MouseEventFlags.MOVE;

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
                Thread.Sleep(80);

                structInput.mkhi.mi.dwFlags = NativeMethods.MouseEventFlags.LEFTDOWN
                    | NativeMethods.MouseEventFlags.LEFTUP;

                NativeMethods.SendInput(1, ref structInput, SizeOfInput);
                SleepOfMouseInputReaction();
                if (doubleClick)
                {
                    NativeMethods.SendInput(1, ref structInput, SizeOfInput);
                    SleepOfMouseInputReaction();
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

        private static void SleepOfMouseInputReaction()
        {
            Thread.Sleep(Rand.Next(100, 150));
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

        public static async Task CloseBotProcessAsync(Process proc, CharacterProfile profile)
        {
            var procName = proc.ProcessName;
            profile.Log("Attempting to close {0}", procName);

            proc.CloseMainWindow();
            if (await WaitForProcessToExitAsync(proc, TimeSpan.FromSeconds(10)))
            {
                profile.Log("Successfully closed {0} gracefully", procName);
                return;
            }

            profile.Log("Killing {0}", procName);
            proc.Kill();
        }

        private static async Task<bool> WaitForProcessToExitAsync(Process process, TimeSpan waitTimespan)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < waitTimespan)
            {
                if (process.HasExitedSafe())
                    return true;

                await Task.Delay(100);
            }
            return false;
        }
    }
}