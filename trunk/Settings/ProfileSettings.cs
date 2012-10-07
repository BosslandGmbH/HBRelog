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

using System.ComponentModel;

namespace HighVoltz.HBRelog.Settings
{
    public class ProfileSettings : INotifyPropertyChanged
    {
        public HonorbuddySettings HonorbuddySettings { get; set; }
        public WowSettings WowSettings { get; set; }
        public ProfileSettings()
        {
            HonorbuddySettings = new HonorbuddySettings();
            WowSettings = new WowSettings();
            ProfileName = string.Empty;
            IsEnabled = true;
        }

        private string _profileName;
        /// <summary>
        /// Name of Profile
        /// </summary>
        public string ProfileName
        {
            get { return _profileName; }
            set { _profileName = value; NotifyPropertyChanged("ProfileName"); }
        }

        private bool _isEnabled;
        /// <summary>
        /// Profile is Enabled
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                NotifyPropertyChanged("IsEnabled");
            }
        }

        public ProfileSettings ShadowCopy()
        {
            var settings = (ProfileSettings)MemberwiseClone();
            settings.WowSettings = WowSettings.ShadowCopy();
            settings.HonorbuddySettings = HonorbuddySettings.ShadowCopy();
            return settings;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
            if (HbRelogManager.Settings != null)
                HbRelogManager.Settings.QueueSave();
        }

    }
}
