using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace HighVoltz.Launcher
{
    public class Program
	{
        [STAThread]
        public static int Main(params string[] args)
		{
<<<<<<< HEAD
            string programType = Console.ReadLine();
            string programPath = Console.ReadLine();

            bool startHBRelog = programType.Equals("HBRelog", StringComparison.OrdinalIgnoreCase);
            if (!startHBRelog && !programType.Equals("WoW", StringComparison.OrdinalIgnoreCase))
                return -2;

            if (startHBRelog)
            {
                var hbRelogInstallPath = Path.GetDirectoryName(programPath);
                var setup = new AppDomainSetup
                {
                    ApplicationBase = hbRelogInstallPath,
                    PrivateBinPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                };
                return AppDomain.CreateDomain("Domain", null, setup).ExecuteAssembly(programPath, args);
            }
            else
            {
                var wowArgs = string.Join(" ", args.Select(a => $"\"{a}\""));
                Helpers.CreateProcessAsStandardUser(programPath, wowArgs, true);
            }
            return 0;
=======
			if (args.Length < 1)
			{
				Console.WriteLine("You must provide a path to a program to launch");
				return -1;
			}
			string programPath = args[0];
			string arg = args.Length > 1 ? args[1] : "";
			Helpers.CreateProcessAsStandardUser(programPath, arg, true);
			return 0;
>>>>>>> 20671d1f80b24cdac66d80c2bcc94ae837a0aa5d
		}

	}
}
