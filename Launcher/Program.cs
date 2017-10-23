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
		}

	}
}
