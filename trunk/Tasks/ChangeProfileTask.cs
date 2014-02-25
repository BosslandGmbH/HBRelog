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

using System.IO;
using System.Xml.Serialization;
using HighVoltz.HBRelog.Controls;

namespace HighVoltz.HBRelog.Tasks
{
    public class ChangeProfileTask : BMTask
    {
        public ChangeProfileTask()
        {
            ProfilePath = "";
            Bot = "";
        }

        [XmlIgnore]
        public override string Name
        {
            get { return "Change HB Profile"; }
        }
        [XmlIgnore]
        override public string Help { get { return "Loads a Honorbuddy profile"; } }

        string _toolTip;
        [XmlIgnore]
        public override string ToolTip
        {
            get
            {
                return _toolTip ?? (ToolTip = string.Format("ChangeProfile {0}{1}", Path.GetFileName(ProfilePath),
                                    !string.IsNullOrEmpty(Bot) ? " Bot: " + Bot : null));
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

        [TaskEditor.CustomTaskEditControl(typeof(ProfilePathEditControl))]
        public string ProfilePath { get; set; }
        public string Bot { get; set; }
        public override void Pulse()
        {
            if (File.Exists(ProfilePath))
            {
                Profile.Log("Loading Honorbuddy profile: {0} and {1}",
                    Profile.Settings.ProfileName, ProfilePath, !string.IsNullOrEmpty(Bot) ? "switching to bot " + Bot : "using current bot");
				Profile.Status = "Changing to Honorbuddy profile: " + Path.GetFileNameWithoutExtension(ProfilePath);
                Profile.TaskManager.HonorbuddyManager.Stop();
                var hbSettings = Profile.Settings.HonorbuddySettings.ShadowCopy();
                hbSettings.HonorbuddyProfile = ProfilePath;
                if (!string.IsNullOrEmpty(Bot))
                    hbSettings.BotBase = Bot;
                Profile.TaskManager.HonorbuddyManager.SetSettings(hbSettings);
                Profile.TaskManager.HonorbuddyManager.Start();
            }
            else
            {
				Profile.Err("Unable to find Honorbuddy profile {0}", ProfilePath);
            }
            IsDone = true;
        }

        public class ProfilePathEditControl : FileInputBox, TaskEditor.ICustomTaskEditControlDataBound
        {
            ChangeProfileTask _task;
            public ProfilePathEditControl()
            {
				Title = "Browse to and select your Honorbuddy profile";
                DefaultExt = ".xml";
                Filter = ".xml|*.xml";
            }
            void TaskEditor.ICustomTaskEditControlDataBound.SetBinding(BMTask source, string path)
            {
                _task = (ChangeProfileTask)source;
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
    }
}
