using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using HighVoltz.HBRelog.FiniteStateMachine;
using ICSharpCode.SharpZipLib.Zip;

namespace HighVoltz.HBRelog.Honorbuddy.States
{
    class CopyPluginToHonorbuddyPathState : State
    {
        #region Fields

        private readonly HonorbuddyManager _hbManager;
        bool _pluginIsUptodate;
        #endregion

        #region Constructors

        public CopyPluginToHonorbuddyPathState(HonorbuddyManager hbManager)
        {
            _hbManager = hbManager;
        }

        #endregion


        #region State Members

        public override int Priority
        {
            get { return 900; }
        }

        public override bool NeedToRun
        {
            get { return _hbManager.IsRunning && (_hbManager.BotProcess == null || _hbManager.BotProcess.HasExited) && !_pluginIsUptodate; }
        }

        public override void Run()
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
            _pluginIsUptodate = true;
        }

        #endregion
    }
}
