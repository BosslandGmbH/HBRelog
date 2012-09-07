using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace HighVoltz.HBRelog
{
    public class NativeMethods
    {
        // credits to http://www.pinvoke.net/ for most of this.

        #region ConnectionStates enum

        [Flags]
        public enum ConnectionStates
        {
            Modem = 0x1,
            Lan = 0x2,
            Proxy = 0x4,
            RasInstalled = 0x10,
            Offline = 0x20,
            Configured = 0x40,
        }

        #endregion

        #region Protection enum

        public enum Protection
        {
            PageNoaccess = 0x01,
            PageReadonly = 0x02,
            PageReadwrite = 0x04,
            PageWritecopy = 0x08,
            PageExecute = 0x10,
            PageExecuteRead = 0x20,
            PageExecuteReadwrite = 0x40,
            PageExecuteWritecopy = 0x80,
            PageGuard = 0x100,
            PageNocache = 0x200,
            PageWritecombine = 0x400
        }

        #endregion

        #region EnumWindows

        #region Delegates

        /// <summary>
        ///   Delegate for the EnumChildWindows method
        /// </summary>
        /// <param name="hWnd"> Window handle </param>
        /// <param name="parameter"> Caller-defined variable; we use it for a pointer to our list </param>
        /// <returns> True to continue enumerating, false to bail. </returns>
        public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

        #endregion

        [SuppressUnmanagedCodeSecurity, DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string libraryName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumWindowProc lpfn, IntPtr lParam);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr i);

        public static List<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(
                    thread.Id, (hWnd, lParam) =>
                                   {
                                       handles.Add(hWnd);
                                       return true;
                                   }, IntPtr.Zero);

            return handles;
        }

        /// <summary>
        ///   Returns a list of child windows
        /// </summary>
        /// <param name="parent"> Parent of the windows to return </param>
        /// <returns> List of child windows </returns>
        public static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            var result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                EnumChildWindows(parent, EnumWindow, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        /// <summary>
        ///   Callback method to be used when enumerating windows.
        /// </summary>
        /// <param name="handle"> Handle of the next window </param>
        /// <param name="pointer"> Pointer to a GCHandle that holds a reference to the list to fill </param>
        /// <returns> True to continue the enumeration, false to bail </returns>
        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            var list = gch.Target as List<IntPtr>;
            if (list == null)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }
            list.Add(handle);
            //  You can modify this to check to see if you want to cancel the operation, then return a null here
            return true;
        }

        #endregion

        #region SetWindowPosFlags enum

        [Flags]
        public enum SetWindowPosFlags : uint
        {
            // ReSharper disable InconsistentNaming
            /// <summary>
            ///   If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window. This prevents the calling thread from blocking its execution while other threads process the request.
            /// </summary>
            SWP_ASYNCWINDOWPOS = 0x4000,

            /// <summary>
            ///   Prevents generation of the WM_SYNCPAINT message.
            /// </summary>
            SWP_DEFERERASE = 0x2000,

            /// <summary>
            ///   Draws a frame (defined in the window's class description) around the window.
            /// </summary>
            SWP_DRAWFRAME = 0x0020,

            /// <summary>
            ///   Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE is sent only when the window's size is being changed.
            /// </summary>
            SWP_FRAMECHANGED = 0x0020,

            /// <summary>
            ///   Hides the window.
            /// </summary>
            SWP_HIDEWINDOW = 0x0080,

            /// <summary>
            ///   Does not activate the window. If this flag is not set, the window is activated and moved to the top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter parameter).
            /// </summary>
            SWP_NOACTIVATE = 0x0010,

            /// <summary>
            ///   Discards the entire contents of the client area. If this flag is not specified, the valid contents of the client area are saved and copied back into the client area after the window is sized or repositioned.
            /// </summary>
            SWP_NOCOPYBITS = 0x0100,

            /// <summary>
            ///   Retains the current position (ignores X and Y parameters).
            /// </summary>
            SWP_NOMOVE = 0x0002,

            /// <summary>
            ///   Does not change the owner window's position in the Z order.
            /// </summary>
            SWP_NOOWNERZORDER = 0x0200,

            /// <summary>
            ///   Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent window uncovered as a result of the window being moved. When this flag is set, the application must explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
            /// </summary>
            SWP_NOREDRAW = 0x0008,

            /// <summary>
            ///   Same as the SWP_NOOWNERZORDER flag.
            /// </summary>
            SWP_NOREPOSITION = 0x0200,

            /// <summary>
            ///   Prevents the window from receiving the WM_WINDOWPOSCHANGING message.
            /// </summary>
            SWP_NOSENDCHANGING = 0x0400,

            /// <summary>
            ///   Retains the current size (ignores the cx and cy parameters).
            /// </summary>
            SWP_NOSIZE = 0x0001,

            /// <summary>
            ///   Retains the current Z order (ignores the hWndInsertAfter parameter).
            /// </summary>
            SWP_NOZORDER = 0x0004,

            /// <summary>
            ///   Displays the window.
            /// </summary>
            SWP_SHOWWINDOW = 0x0040,
            // ReSharper restore InconsistentNaming
        }

        #endregion

        public const int MaxPath = 260;
        public const int MaxAlternate = 14;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(HandleRef hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetFileAttributes(string lpFileName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
                                               SetWindowPosFlags uFlags);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetGetConnectedState(out int lpdwFlags, int dwReserved);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindFirstFile(string lpFileName, out Win32FindData lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool FindNextFile(IntPtr hFindFile, out Win32FindData lpFindFileData);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualProtect(uint lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

        [DllImport("user32.dll")]
        public static extern int SetWindowText(IntPtr hWnd, string text);

        public static string GetWindowText(IntPtr hWnd)
        {
            // Allocate correct string length first
            int length = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        #region Nested Type: FILETIME

        [StructLayout(LayoutKind.Sequential)]
        public struct Filetime
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        };

        #endregion

        #region Nested Type: Rect

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left; // x position of upper-left corner
            public int Top; // y position of upper-left corner
            public int Right; // x position of lower-right corner
            public int Bottom; // y position of lower-right corner
        }

        #endregion

        #region Nested Type: WIN32_FIND_DATA

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct Win32FindData
        {
            public FileAttributes dwFileAttributes;
            public Filetime ftCreationTime;
            public Filetime ftLastAccessTime;
            public Filetime ftLastWriteTime;
            public uint nFileSizeHigh; //changed all to uint from int, otherwise you run into unexpected overflow
            public uint nFileSizeLow; //| http://www.pinvoke.net/default.aspx/Structures/WIN32_FIND_DATA.html
            public uint dwReserved0; //|
            public uint dwReserved1; //v
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxAlternate)] public string cAlternate;
        }

        #endregion

        #region Nested Type: ShowWndowCommands

        public enum ShowWindowCommands
        {
            /// <summary>
            ///   Hides the window and activates another window.
            /// </summary>
            Hide = 0,

            /// <summary>
            ///   Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
            /// </summary>
            Normal = 1,

            /// <summary>
            ///   Activates the window and displays it as a minimized window.
            /// </summary>
            ShowMinimized = 2,

            /// <summary>
            ///   Maximizes the specified window.
            /// </summary>
            Maximize = 3, // is this the right value?
            /// <summary>
            ///   Activates the window and displays it as a maximized window.
            /// </summary>
            ShowMaximized = 3,

            /// <summary>
            ///   Displays a window in its most recent size and position. This value is similar to <see
            ///    cref="Win32.ShowWindowCommand.Normal" /> , except the window is not activated.
            /// </summary>
            ShowNoActivate = 4,

            /// <summary>
            ///   Activates the window and displays it in its current size and position.
            /// </summary>
            Show = 5,

            /// <summary>
            ///   Minimizes the specified window and activates the next top-level window in the Z order.
            /// </summary>
            Minimize = 6,

            /// <summary>
            ///   Displays the window as a minimized window. This value is similar to <see cref="Win32.ShowWindowCommand.ShowMinimized" /> , except the window is not activated.
            /// </summary>
            ShowMinNoActive = 7,

            /// <summary>
            ///   Displays the window in its current size and position. This value is similar to <see cref="Win32.ShowWindowCommand.Show" /> , except the window is not activated.
            /// </summary>
            ShowNA = 8,

            /// <summary>
            ///   Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
            /// </summary>
            Restore = 9,

            /// <summary>
            ///   Sets the show state based on the SW_* value specified in the STARTUPINFO structure passed to the CreateProcess function by the program that started the application.
            /// </summary>
            ShowDefault = 10,

            /// <summary>
            ///   <b>Windows 2000/XP:</b> Minimizes a window, even if the thread that owns the window is not responding. This flag should only be used when minimizing windows from a different thread.
            /// </summary>
            ForceMinimize = 11
        }

        #endregion
    }
}