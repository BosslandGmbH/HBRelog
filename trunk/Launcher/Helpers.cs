using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HighVoltz.Launcher
{
	public class Helpers
	{

		[DllImport("kernel32.dll")]
		static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
		[DllImport("kernel32.dll")]
		static extern uint SuspendThread(IntPtr hThread);
		[DllImport("kernel32.dll")]
		static extern int ResumeThread(IntPtr hThread);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool CreateProcess(string lpApplicationName,
		   string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
		   ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFO lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation);

		public static int? StartProcessSuspended(string fileName, string args)
		{
			const uint CREATE_SUSPENDED = 0x00000004;

			var pInfo = new PROCESS_INFORMATION();
			var sInfo = new STARTUPINFO();
			var pSec = new SECURITY_ATTRIBUTES();
			var tSec = new SECURITY_ATTRIBUTES();
			pSec.nLength = Marshal.SizeOf(pSec);
			tSec.nLength = Marshal.SizeOf(tSec);

			var result = CreateProcess(
				null,
				fileName + " " + args,
				ref pSec,
				ref tSec,
				false,
				CREATE_SUSPENDED,
				IntPtr.Zero,
				null,
				ref sInfo,
				out pInfo);
			return result ? (int?)pInfo.dwProcessId : null;
		}

		public static void SuspendProcess(int pid)
		{
			Process proc = Process.GetProcessById(pid);

			if (proc.ProcessName == string.Empty)
				return;

			foreach (ProcessThread pT in proc.Threads)
			{
				IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

				if (pOpenThread == IntPtr.Zero)
				{
					break;
				}

				SuspendThread(pOpenThread);
			}
		}

		public static void ResumeProcess(int pid)
		{
			Process proc = Process.GetProcessById(pid);

			if (proc.ProcessName == string.Empty)
				return;

			foreach (ProcessThread pT in proc.Threads)
			{
				IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

				if (pOpenThread == IntPtr.Zero)
				{
					break;
				}

				ResumeThread(pOpenThread);
			}
		}

		[Flags]
		public enum ThreadAccess : int
		{
			TERMINATE = (0x0001),
			SUSPEND_RESUME = (0x0002),
			GET_CONTEXT = (0x0008),
			SET_CONTEXT = (0x0010),
			SET_INFORMATION = (0x0020),
			QUERY_INFORMATION = (0x0040),
			SET_THREAD_TOKEN = (0x0080),
			IMPERSONATE = (0x0100),
			DIRECT_IMPERSONATION = (0x0200)
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		struct STARTUPINFO
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

		[StructLayout(LayoutKind.Sequential)]
		public struct SECURITY_ATTRIBUTES
		{
			public int nLength;
			public unsafe byte* lpSecurityDescriptor;
			public int bInheritHandle;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct PROCESS_INFORMATION
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public int dwProcessId;
			public int dwThreadId;
		}
	}
}
