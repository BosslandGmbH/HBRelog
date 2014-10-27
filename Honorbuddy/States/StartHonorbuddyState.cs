using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    class StartHonorbuddyState : State
    {
        #region Fields

        private readonly HonorbuddyManager _hbManager;
        #endregion

        #region Constructors

        public StartHonorbuddyState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion


        #region State Members

        public override int Priority
        {
            get { return 800; }
        }

        public override bool NeedToRun
        {
            get { return _hbManager.Profile.IsRunning && _hbManager.BotProcess == null && _hbManager.Profile.TaskManager.WowManager.GameProcess != null && !_hbManager.Profile.TaskManager.WowManager.GameProcess.HasExitedSafe(); }
        }

        public override void Run()
        {
            // remove internet zone restrictions from Honorbuddy.exe if it exists
            Utility.UnblockFileIfZoneRestricted(_hbManager.Settings.HonorbuddyPath);
            // we need to delay starting honorbuddy for a few seconds if another instance from same path was started a few seconds ago
	        if (HonorbuddyManager.HBStartupManager.CanStart(_hbManager.Settings.HonorbuddyPath))
	        {
		        CopyHBRelogHelperPluginOver();
				_hbManager.StartHonorbuddy();
	        }

        }

	    void CopyHBRelogHelperPluginOver()
	    {
			// remove internet zone restrictions from Honorbuddy.exe if it exists
			Utility.UnblockFileIfZoneRestricted(_hbManager.Settings.HonorbuddyPath);
			// check if we need to copy over plugin.
			using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("HighVoltz.HBRelog.HBPlugin.HBRelogHelper.cs")))
			{
				string pluginString = reader.ReadToEnd();
				// copy the HBPlugin over to the Honorbuddy plugin folder if it doesn't exist.
				// or length doesn't match with the version in resource.
				string pluginFolder = Path.Combine(Path.GetDirectoryName(_hbManager.Settings.HonorbuddyPath), "Plugins\\HBRelogHelper");
				if (!Directory.Exists(pluginFolder))
					Directory.CreateDirectory(pluginFolder);

				string pluginPath = Path.Combine(pluginFolder, "HBRelogHelper.cs");

				var fi = new FileInfo(pluginPath);
				if (!fi.Exists || fi.Length != pluginString.Length)
				{
					File.WriteAllText(pluginPath, pluginString);
				}
			}
	    }
        #endregion
    }
}
