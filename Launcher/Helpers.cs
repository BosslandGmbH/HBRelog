using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HighVoltz.Launcher
{
	public class Helpers
	{
        #region Imports
        
        [DllImport("kernel32.dll")]
		internal static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll")]
        internal static extern uint SuspendThread(IntPtr hThread);

		[DllImport("kernel32.dll")]
        internal static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        //   [DllImport("kernel32.dll", SetLastError = true)]
        //   private static extern bool CreateProcess(string lpApplicationName,
        //string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
        //ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles,
        //uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
        //[In] ref STARTUPINFO lpStartupInfo,
        //out ProcessInfo lpProcessInformation);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr hToken, int dwLogonFlags, string applicationName, string commandLine,
            uint creationFlags, IntPtr environment, IntPtr currentDirectory,
            [In] ref StartupInfo startupInfo, out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreateProcess(string lpApplicationName,
               string lpCommandLine, ref SecurityAttributes lpProcessAttributes,
               ref SecurityAttributes lpThreadAttributes, bool bInheritHandles,
               uint dwCreationFlags, IntPtr lpEnvironment, string currentDirectory,
               [In] ref StartupInfo startupInfo,
               out ProcessInformation processInformation);

        [DllImport("advapi32.dll", SetLastError = true)]
        private unsafe static extern bool GetTokenInformation(IntPtr tokenHandle, TokenInformationClass TokenInformationClass,
            void* tokenInformation, uint tokenInformationLength, out uint returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, TokenAcess desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private extern static bool DuplicateTokenEx(
            IntPtr hExistingToken,
            TokenAcess desiredAccess,
            IntPtr tokenAttributes,
            SecurityImpersonationLevel impersonationLevel,
            TokenType tokenType,
            out IntPtr hNewToken);


        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out Luid luid);

        // Use this signature if you do not want the previous state
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle,
           [MarshalAs(UnmanagedType.Bool)]bool disableAllPrivileges,
           ref TokenPrivileges newState,
           UInt32 bufferLengthInBytes,
           IntPtr previousState,
           IntPtr returnLengthInBytes);


        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        #endregion

        #region Embedded Types

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr ProcessHandle;
            public IntPtr ThreadHandle;
            public int ProcessId;
            public int ThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityAttributes
        {
            public int Length;
            public unsafe byte* SecurityDescriptor;
            public int InheritHandle;
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct StartupInfo
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Luid
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public Luid Luid;
            public uint Attributes;
        }


        public enum TokenInformationClass
        {
            User = 1,
            Groups,
            Privileges,
            Owner,
            PrimaryGroup,
            DefaultDacl,
            Source,
            Type,
            ImpersonationLevel,
            Statistics,
            RestrictedSids,
            SessionId,
            GroupsAndPrivileges,
            SessionReference,
            SandBoxInert,
            AuditPolicy,
            Origin,
            ElevationType,
            LinkedToken,
            Elevation,
            HasRestrictions,
            AccessInformation,
            VirtualizationAllowed,
            VirtualizationEnabled,
            IntegrityLevel,
            UIAccess,
            MandatoryPolicy,
            LogonSid,
            MaxTokenInfoClass
        }

        private enum SecurityImpersonationLevel
        {
            Anonymous,
            Identification,
            Impersonation,
            Delegation
        }

        private enum TokenType
        {
            Primary = 1,
            Impersonation
        }

        private enum TokenAcess
        {
            StandardRightsRequired = 0x000F0000,
            StandaredRightsRead = 0x00020000,
            AssignPrimary = 0x0001,
            Duplicate = 0x0002,
            Impersonate = 0x0004,
            Query = 0x0008,
            QuerySource = 0x0010,
            AdjustPrivileges = 0x0020,
            AdjustGroups = 0x0040,
            AdjustDefault = 0x0080,
            AdjustSessionId = 0x0100,
            Read = (StandardRightsRequired | Query),
            AllAccess = (StandardRightsRequired | AssignPrimary |
                Duplicate | Impersonate | Query | QuerySource |
                AdjustPrivileges | AdjustGroups | AdjustDefault |
                AdjustSessionId),
        }

        private enum TokenElevationType
        {
            Default = 1,
            Full,
            Limited
        }

        [Flags]
        public enum ThreadAccess : int
        {
            Terminate = (0x0001),
            SuspendResume = (0x0002),
            GetContext = (0x0008),
            SetContext = (0x0010),
            SetInformation = (0x0020),
            QueryInformation = (0x0040),
            SetThreadToken = (0x0080),
            Impersonate = (0x0100),
            DirectImpersonation = (0x0200)
        }

        #endregion

        #region Constants

        private const string IncreaseQuotaName = "SeIncreaseQuotaPrivilege";
        private const int PrivilegeEnabled = 0x00000002;
        private const int ProcessQueryInformation = 0x0400;
        private const uint CreateSuspended = 0x00000004;

        public const string UacRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
        public const string UacRegistryValue = "EnableLUA";

        #endregion

        public static bool IsUacEnabled
        {
            get
            {
                //Check the HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\System\EnableLUA registry value.
                RegistryKey key = Registry.LocalMachine.OpenSubKey(UacRegistryKey, false);
                return key?.GetValue(UacRegistryValue)?.Equals(1) ?? false;
            }
        }

        private static int StartProcessSuspended(string exePath, string arguments)
        {
            var pInfo = new ProcessInformation();
            var sInfo = new StartupInfo();
            var pSec = new SecurityAttributes();
            var tSec = new SecurityAttributes();
            pSec.Length = Marshal.SizeOf(pSec);
            tSec.Length = Marshal.SizeOf(tSec);

            if (!CreateProcess(null, exePath + " " + arguments, ref pSec, ref tSec, false, CreateSuspended, IntPtr.Zero, null, ref sInfo, out pInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return pInfo.ProcessId;
        }

        // Based on http://blogs.microsoft.co.il/sasha/2009/07/09/launch-a-process-as-standard-user-from-an-elevated-process/
        public static int CreateProcessAsStandardUserSuspended(string exePath, string arguments)
        {
            if (!IsUacEnabled)
                return StartProcessSuspended(exePath, arguments);

            //Enable SeIncreaseQuotaPrivilege in this process.  (This requires administrative privileges.)
            IntPtr hProcessToken = IntPtr.Zero;
            var currentProcess = Process.GetCurrentProcess();
            if (!OpenProcessToken(currentProcess.Handle, TokenAcess.AdjustPrivileges, out hProcessToken))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                TokenPrivileges tkp = new TokenPrivileges();
                tkp.PrivilegeCount = 1;
                if (!LookupPrivilegeValue(null, IncreaseQuotaName, out Luid luid))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                tkp.Attributes = PrivilegeEnabled;

                if (!AdjustTokenPrivileges(hProcessToken, false, ref tkp, 0, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                CloseHandle(hProcessToken);
            }

            //Get window handle representing the desktop shell.  This might not work if there is no shell window, or when
            //using a custom shell.  Also note that we're assuming that the shell is not running elevated.
            IntPtr hShellWnd = GetShellWindow();
            if (hShellWnd == IntPtr.Zero)
                throw new InvalidOperationException("Unable to locate shell window; you might be using a custom shell");


            int shellPid;
            GetWindowThreadProcessId(hShellWnd, out shellPid);
            if (shellPid == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            //Open the desktop shell process in order to get the process token.
            IntPtr hShellProcess = OpenProcess(ProcessQueryInformation, false, shellPid);
            if (hShellProcess == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());


            IntPtr hShellProcessToken = IntPtr.Zero;
            IntPtr hPrimaryToken = IntPtr.Zero;
            try
            {
                //Get the process token of the desktop shell.
                if (!OpenProcessToken(hShellProcess, TokenAcess.Duplicate, out hShellProcessToken))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                //Duplicate the shell's process token to get a primary token.
                const TokenAcess tokenRights = TokenAcess.Query | TokenAcess.AssignPrimary | TokenAcess.Duplicate | TokenAcess.AdjustDefault | TokenAcess.AdjustSessionId;
                if (!DuplicateTokenEx(hShellProcessToken, tokenRights, IntPtr.Zero, SecurityImpersonationLevel.Impersonation, TokenType.Primary, out hPrimaryToken))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                //Start the target process with the new token.
                StartupInfo startupInfo = new StartupInfo();
                ProcessInformation pi = new ProcessInformation();

                if (!CreateProcessWithTokenW(hPrimaryToken, 0, exePath, exePath + " " + arguments, CreateSuspended, IntPtr.Zero, IntPtr.Zero, ref startupInfo, out pi))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                CloseHandle(pi.ProcessHandle);
                CloseHandle(pi.ThreadHandle);
                return pi.ProcessId;
            }
            finally
            {
                if (hShellProcessToken != IntPtr.Zero)
                    CloseHandle(hShellProcessToken);

                if (hPrimaryToken != IntPtr.Zero)
                    CloseHandle(hPrimaryToken);

                if (hShellProcess != IntPtr.Zero)
                    CloseHandle(hShellProcess);
            }
        }

        public static bool IsProcessElevated(Process process)
        {
            return GetProcessTokenElevationType(process) == TokenElevationType.Full;
        }

        private static unsafe TokenElevationType GetProcessTokenElevationType(Process process)
        {
            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, TokenAcess.Query, out hToken))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                TokenElevationType elevationType;
                uint size;
                if (!GetTokenInformation(hToken, TokenInformationClass.ElevationType, &elevationType, 4, out size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                return elevationType;
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        public static void SuspendProcess(int pid)
		{
			Process proc = Process.GetProcessById(pid);

			if (proc.ProcessName == string.Empty)
				return;

			foreach (ProcessThread pT in proc.Threads)
			{
				IntPtr pOpenThread = OpenThread(ThreadAccess.SuspendResume, false, (uint)pT.Id);

				if (pOpenThread == IntPtr.Zero)
					break;

				SuspendThread(pOpenThread);
                CloseHandle(pOpenThread);
            }
            proc.Dispose();
        }

		public static void ResumeProcess(int pid)
		{
			Process proc = Process.GetProcessById(pid);

			if (proc.ProcessName == string.Empty)
				return;

			foreach (ProcessThread pT in proc.Threads)
			{
				IntPtr pOpenThread = OpenThread(ThreadAccess.SuspendResume, false, (uint)pT.Id);

				if (pOpenThread == IntPtr.Zero)
					break;

				ResumeThread(pOpenThread);
                CloseHandle(pOpenThread);
            }

            proc.Dispose();
        }


	}
}
