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

using HighVoltz.HBRelog.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace HighVoltz.HBRelog.Settings
{
    public class ProfileSettings : SettingsBase
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
            get => _profileName;
            set => NotifyPropertyChanged(ref _profileName, ref value, nameof(ProfileName));
        }

        private bool _isEnabled;
        /// <summary>
        /// Profile is Enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => NotifyPropertyChanged(ref _isEnabled, ref value, nameof(IsEnabled));
        }

        public override SettingsBase ShadowCopy()
        {
            var settings = (ProfileSettings)MemberwiseClone();
            settings.WowSettings = (WowSettings)WowSettings.ShadowCopy();
            settings.HonorbuddySettings = (HonorbuddySettings)HonorbuddySettings.ShadowCopy();
            return settings;
        }

        public override void LoadFromXml(XElement element)
        {
            try
            {
                IsLoaded = false;
                ProfileName = GetElementValue<string>(element.Element("ProfileName"));
                IsEnabled = GetElementValue<bool>(element.Element("IsEnabled"));

                // Wow Settings 
                XElement wowSettingsElement = element.Element("WowSettings");
                if (wowSettingsElement != null)
                    WowSettings.LoadFromXml(wowSettingsElement);

                // Honorbuddy Settings
                XElement hbSettingsElement = element.Element("HonorbuddySettings");
                if (hbSettingsElement != null)
                    HonorbuddySettings.LoadFromXml(hbSettingsElement);
            }
            finally
            {
                IsLoaded = true;
            }
        }

        public override XElement ConvertToXml()
        {
            var xml = new XElement("Settings");
            xml.Add(new XElement("ProfileName", ProfileName));
            xml.Add(new XElement("IsEnabled", IsEnabled));

            // Wow Settings 
            var wowSettingsElement = WowSettings.ConvertToXml();
            xml.Add(wowSettingsElement);

            // Honorbuddy Settings
            var hbSettingsElement = HonorbuddySettings.ConvertToXml();
            xml.Add(hbSettingsElement);

            return xml;
        }
    }
}
