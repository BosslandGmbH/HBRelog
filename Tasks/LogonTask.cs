/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System.Xml.Serialization;
using HighVoltz.HBRelog.Controls;

namespace HighVoltz.HBRelog.Tasks
{
    public class LogonTask : BMTask
    {
        public LogonTask()
        {
			HonorbuddyArgs = HonorbuddyPath = CustomClass = ProfilePath = BotBase = Server = CharacterName = "";
        }

        [XmlIgnore]
        public override string Name
        {
            get { return "Logon"; }
        }

        [XmlIgnore]
        override public string Help { get { return "Logs on a Character in Wow"; } }

        string _toolTip;
        [XmlIgnore]
        public override string ToolTip
        {
            get
            {
                return _toolTip ?? (ToolTip = string.Format("Logon: {0} {1}", CharacterName,
                                                    !string.IsNullOrEmpty(Server) ? "- " + Server : null));
            }
            set
            {
                if (value != _toolTip)
                {
                    _toolTip = value;
                    OnPropertyChanged("ToolTip");
                }
            }
        }

        public string CharacterName { get; set; }
        public string Server { get; set; }
        public string BotBase { get; set; }
        public string CustomClass { get; set; }
        [TaskEditor.CustomTaskEditControl(typeof(ProfilePathEditControl))]
        public string ProfilePath { get; set; }
        [TaskEditor.CustomTaskEditControl(typeof(HBPathEditControl))]
        public string HonorbuddyPath { get; set; }
		public string HonorbuddyArgs { get; set; }

        bool _runOnce;
        public override void Pulse()
        {
            if (!_runOnce)
            {
                var wowSettings = Profile.Settings.WowSettings.ShadowCopy();
                var hbSettings = Profile.Settings.HonorbuddySettings.ShadowCopy();

                if (!string.IsNullOrEmpty(CharacterName))
                    wowSettings.CharacterName = CharacterName;
                if (!string.IsNullOrEmpty(Server))
                    wowSettings.ServerName = Server;

                if (!string.IsNullOrEmpty(BotBase))
                    hbSettings.BotBase = BotBase;
                if (!string.IsNullOrEmpty(ProfilePath))
                    hbSettings.HonorbuddyProfile = ProfilePath;
                if (!string.IsNullOrEmpty(CustomClass))
                    hbSettings.CustomClass = CustomClass;
				if (!string.IsNullOrEmpty(HonorbuddyPath))
					hbSettings.HonorbuddyPath = HonorbuddyPath;
				if (!string.IsNullOrEmpty(HonorbuddyArgs))
					hbSettings.HonorbuddyArgs = HonorbuddyArgs;

                Profile.Log("Logging on different character.");
                Profile.Status = "Logging on a different character";
                Profile.TaskManager.HonorbuddyManager.Stop();
                Profile.TaskManager.WowManager.Stop();
                // assign new settings
                Profile.TaskManager.HonorbuddyManager.SetSettings(hbSettings);
                Profile.TaskManager.WowManager.SetSettings(wowSettings);
                Profile.TaskManager.WowManager.Start();
                _runOnce = true;
            }
            if (Profile.TaskManager.WowManager.InGame)
                IsDone = true;
        }

        public override void Reset()
        {
            base.Reset();
            _runOnce = false;
        }

        public class ProfilePathEditControl : FileInputBox, TaskEditor.ICustomTaskEditControlDataBound
        {
            LogonTask _task;
            public ProfilePathEditControl()
            {
                Title = "Browse to and select your profile";
                DefaultExt = ".xml";
                Filter = ".xml|*.xml";
            }
            void TaskEditor.ICustomTaskEditControlDataBound.SetBinding(BMTask source, string path)
            {
                _task = (LogonTask)source;
                // binding issues.. so just hooking an event.
                // Binding binding = new Binding(path);
                // binding.Source = source;
                // SetBinding(FileNameProperty, binding);
            }

            void TaskEditor.ICustomTaskEditControlDataBound.SetValue(object value)
            {
                FileName = value.ToString();
                FileNameChanged += ProfilePathEditControlFileNameChanged;
            }

            void ProfilePathEditControlFileNameChanged(object sender, System.Windows.RoutedEventArgs e)
            {
                _task.ProfilePath = FileName;
            }
        }

        public class HBPathEditControl : FileInputBox, TaskEditor.ICustomTaskEditControlDataBound
        {
            LogonTask _task;
            public HBPathEditControl()
            {
                Title = "Browse to and your Honorbuddy .exe";
                DefaultExt = ".exe";
                Filter = ".exe|*.exe";
            }
            void TaskEditor.ICustomTaskEditControlDataBound.SetBinding(BMTask source, string path)
            {
                _task = (LogonTask)source;
                // binding issues.. so just hooking an event.
                // Binding binding = new Binding(path);
                // binding.Source = source;
                // SetBinding(FileNameProperty, binding);
            }

            void TaskEditor.ICustomTaskEditControlDataBound.SetValue(object value)
            {
                FileName = value.ToString();
                FileNameChanged += ProfilePathEditControlFileNameChanged;
            }

            void ProfilePathEditControlFileNameChanged(object sender, System.Windows.RoutedEventArgs e)
            {
                _task.HonorbuddyPath = FileName;
            }
        }
    }
}
