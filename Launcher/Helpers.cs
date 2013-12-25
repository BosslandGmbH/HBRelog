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

		[DllImport("kernel32.dll")]
		static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
		[DllImport("kernel32.dll")]
		static extern uint SuspendThread(IntPtr hThread);
		[DllImport("kernel32.dll")]
		static extern int ResumeThread(IntPtr hThread);

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
	}
}
