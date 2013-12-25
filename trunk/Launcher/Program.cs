using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HighVoltz.Launcher
{
	static class Program
	{

		static int Main(params string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("You must provide a path to a program to launch");
				return -1;
			}
			string programPath = args[0];
			string arg = args.Length > 1 ? args[1] : "";
			Helpers.StartProcessSuspended(programPath, arg);
			return 0;
		}

	}
}
