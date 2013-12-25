using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Stub
{
	static class Program
	{

		static void Main(params string[] args)
		{
			if (args.Length < 1)
				throw new ArgumentException("You must provide a path to a program to launch", "args");
			string programPath = args[0];
			string arg = args.Length > 1 ? args[1] : "";
			Process.Start(programPath, arg);
			Thread.Sleep(20000);
		}
	}
}
