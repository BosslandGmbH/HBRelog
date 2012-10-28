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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog.Settings
{
    public class GlobalSettings : INotifyPropertyChanged
    {
        private GlobalSettings()
        {
            CharacterProfiles = new ObservableCollection<CharacterProfile>();
            string settingsFolder = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(settingsFolder))
                Directory.CreateDirectory(settingsFolder);

            // set some default settings
            HBDelay = 3;
            AutoUpdateHB = CheckHbResponsiveness = UseDarkStyle = true;
        }

        public string SettingsPath
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                       "\\HighVoltz\\HBRelog\\Setting.xml";
            }
        }

        public ObservableCollection<CharacterProfile> CharacterProfiles { get; set; }
        // Automatically start all enabled profiles on start
        private bool _autoStart;
        public bool AutoStart
        {
            get { return _autoStart; }
            set { _autoStart = value; NotifyPropertyChanged("AutoStart"); }
        }

        // delay in seconds between starting multiple Wow instance
        private int _wowDelay;
        public int WowDelay
        {
            get { return _wowDelay; }
            set { _wowDelay = value; NotifyPropertyChanged("WowDelay"); }
        }

        // delay in seconds between starting multiple Honorbuddy instance
        private int _hBDelay;
        public int HBDelay
        {
            get { return _hBDelay; }
            set { _hBDelay = value; NotifyPropertyChanged("HBDelay"); }
        }

        // delay in seconds between executing login actions.
        private int _loginDelay;
        public int LoginDelay
        {
            get { return _loginDelay; }
            set { _loginDelay = value; NotifyPropertyChanged("LoginDelay"); }
        }

        private bool _useDarkStyle;
        public bool UseDarkStyle
        {
            get { return _useDarkStyle; }
            set { _useDarkStyle = value; NotifyPropertyChanged("UseDarkStyle"); }
        }

        private bool _checkRealmStatus;
        public bool CheckRealmStatus
        {
            get { return _checkRealmStatus; }
            set { _checkRealmStatus = value; NotifyPropertyChanged("CheckRealmStatus"); }
        }

        private bool _checkHbResponsiveness;
        public bool CheckHbResponsiveness
        {
            get { return _checkHbResponsiveness; }
            set { _checkHbResponsiveness = value; NotifyPropertyChanged("CheckHbResponsiveness"); }
        }

        private bool _autoUpdateHB;
        public bool AutoUpdateHB
        {
            get { return _autoUpdateHB; }
            set { _autoUpdateHB = value; NotifyPropertyChanged("AutoUpdateHB"); }
        }


        private bool _useHBBeta;
        public bool UseHBBeta
        {
            get { return _useHBBeta; }
            set { _useHBBeta = value; NotifyPropertyChanged("UseHBBeta"); }
        }

        private bool _minimizeHbOnStart;
        /// <summary>
        /// Minimizes HB to system tray on start
        /// </summary>
        public bool MinimizeHbOnStart
        {
            get { return _minimizeHbOnStart; }
            set { _minimizeHbOnStart = value; NotifyPropertyChanged("MinimizeHbOnStart"); }
        }

        public string WowVersion { get; set; }

        // offsets
        public uint GameStateOffset { get; set; }
        public uint FrameScriptExecuteOffset { get; set; }
        public uint LastHardwareEventOffset { get; set; }
        public uint GlueStateOffset { get; set; }
        // serializers giving me issues with colections.. so saving stuff manually.
        public void Save()
        {
            try
            {
                var root = new XElement("BotManager");
                root.Add(new XElement("AutoStart", AutoStart));
                root.Add(new XElement("WowDelay", WowDelay));
                root.Add(new XElement("HBDelay", HBDelay));
                root.Add(new XElement("LoginDelay", LoginDelay));
                root.Add(new XElement("UseDarkStyle", UseDarkStyle));
                root.Add(new XElement("CheckRealmStatus", CheckRealmStatus));
                root.Add(new XElement("CheckHbResponsiveness", CheckHbResponsiveness));
                root.Add(new XElement("MinimizeHbOnStart", MinimizeHbOnStart));
                root.Add(new XElement("AutoUpdateHB", AutoUpdateHB));
                root.Add(new XElement("UseHBBeta", UseHBBeta));
                
                root.Add(new XElement("WowVersion", WowVersion));

                root.Add(new XElement("GameStateOffset", GameStateOffset));
                root.Add(new XElement("FrameScriptExecuteOffset", FrameScriptExecuteOffset));
                root.Add(new XElement("LastHardwareEventOffset", LastHardwareEventOffset));
                root.Add(new XElement("GlueStateOffset", GlueStateOffset));

                var characterProfilesElement = new XElement("CharacterProfiles");
                foreach (CharacterProfile profile in CharacterProfiles)
                {
                    var profileElement = new XElement("CharacterProfile");
                    var settingsElement = new XElement("Settings");
                    settingsElement.Add(new XElement("ProfileName", profile.Settings.ProfileName));
                    settingsElement.Add(new XElement("IsEnabled", profile.Settings.IsEnabled));
                    var wowSettingsElement = new XElement("WowSettings");
                    // Wow Settings 
                    wowSettingsElement.Add(new XElement("LoginData", profile.Settings.WowSettings.LoginData));
                    wowSettingsElement.Add(new XElement("PasswordData", profile.Settings.WowSettings.PasswordData));
                    wowSettingsElement.Add(new XElement("AcountName", profile.Settings.WowSettings.AcountName));
                    wowSettingsElement.Add(new XElement("CharacterName", profile.Settings.WowSettings.CharacterName));
                    wowSettingsElement.Add(new XElement("ServerName", profile.Settings.WowSettings.ServerName));
                    wowSettingsElement.Add(new XElement("Region", profile.Settings.WowSettings.Region));
                    wowSettingsElement.Add(new XElement("WowPath", profile.Settings.WowSettings.WowPath));
                    wowSettingsElement.Add(new XElement("WowWindowWidth", profile.Settings.WowSettings.WowWindowWidth));
                    wowSettingsElement.Add(new XElement("WowWindowHeight", profile.Settings.WowSettings.WowWindowHeight));
                    wowSettingsElement.Add(new XElement("WowWindowX", profile.Settings.WowSettings.WowWindowX));
                    wowSettingsElement.Add(new XElement("WowWindowY", profile.Settings.WowSettings.WowWindowY));
                    settingsElement.Add(wowSettingsElement);
                    var hbSettingsElement = new XElement("HonorbuddySettings");
                    // Honorbuddy Settings
                    hbSettingsElement.Add(new XElement("CustomClass", profile.Settings.HonorbuddySettings.CustomClass));
                    hbSettingsElement.Add(new XElement("BotBase", profile.Settings.HonorbuddySettings.BotBase));
                    hbSettingsElement.Add(new XElement("HonorbuddyProfile",
                                                       profile.Settings.HonorbuddySettings.HonorbuddyProfile));
                    hbSettingsElement.Add(new XElement("HonorbuddyPath",
                                                       profile.Settings.HonorbuddySettings.HonorbuddyPath));
                    settingsElement.Add(hbSettingsElement);
                    profileElement.Add(settingsElement);
                    var tasksElement = new XElement("Tasks");

                    foreach (BMTask task in profile.Tasks)
                    {
                        var taskElement = new XElement(task.GetType().Name);
                        // get a list of propertyes that don't have [XmlIgnore] custom attribute attached.
                        List<PropertyInfo> propertyList = task.GetType().GetProperties().
                            Where(
                                pi =>
                                pi.GetCustomAttributesData().All(cad => cad.Constructor.DeclaringType != typeof(XmlIgnoreAttribute))).ToList();
                        foreach (PropertyInfo property in propertyList)
                        {
                            taskElement.Add(new XAttribute(property.Name, property.GetValue(task, null)));
                        }
                        tasksElement.Add(taskElement);
                    }
                    profileElement.Add(tasksElement);
                    characterProfilesElement.Add(profileElement);
                }
                root.Add(characterProfilesElement);
                var xmlSettings = new XmlWriterSettings
                                      {
                                          OmitXmlDeclaration = true,
                                          Indent = true,
                                      };

                using (XmlWriter xmlOutFile = XmlWriter.Create(SettingsPath, xmlSettings))
                {
                    root.Save(xmlOutFile);
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        /// <summary>
        /// Attempts to load settings from file
        /// </summary>
        /// <returns>A GlocalSettings</returns>
        public static GlobalSettings Load()
        {
            var settings = new GlobalSettings();
            try
            {
                if (File.Exists(settings.SettingsPath))
                {
                    XElement root = XElement.Load(settings.SettingsPath);
                    settings.WowVersion = root.Element("WowVersion").Value;
                    settings.AutoStart = GetElementValue<bool>(root.Element("AutoStart"));
                    settings.WowDelay = GetElementValue<int>(root.Element("WowDelay"));
                    settings.HBDelay = GetElementValue(root.Element("HBDelay"), 10);
                    settings.LoginDelay = GetElementValue(root.Element("LoginDelay"), 3);
                    settings.UseDarkStyle = GetElementValue(root.Element("UseDarkStyle"), true);
                    settings.CheckRealmStatus = GetElementValue(root.Element("CheckRealmStatus"), false);
                    settings.CheckHbResponsiveness = GetElementValue(root.Element("CheckHbResponsiveness"), true);
                    settings.AutoUpdateHB = GetElementValue(root.Element("AutoUpdateHB"), true);
                    settings.UseHBBeta = GetElementValue(root.Element("UseHBBeta"), false);
                    settings.MinimizeHbOnStart = GetElementValue(root.Element("MinimizeHbOnStart"), false);

                    settings.GameStateOffset = uint.Parse(root.Element("GameStateOffset").Value);
                    settings.FrameScriptExecuteOffset = uint.Parse(root.Element("FrameScriptExecuteOffset").Value);
                    settings.LastHardwareEventOffset = uint.Parse(root.Element("LastHardwareEventOffset").Value);
                    settings.GlueStateOffset = uint.Parse(root.Element("GlueStateOffset").Value);
                    XElement characterProfilesElement = root.Element("CharacterProfiles");
                    foreach (XElement profileElement in characterProfilesElement.Elements("CharacterProfile"))
                    {
                        var profile = new CharacterProfile();
                        XElement settingsElement = profileElement.Element("Settings");
                        profile.Settings.ProfileName = GetElementValue<string>(settingsElement.Element("ProfileName"));
                        profile.Settings.IsEnabled = GetElementValue<bool>(settingsElement.Element("IsEnabled"));
                        XElement wowSettingsElement = settingsElement.Element("WowSettings");

                        // Wow Settings 
                        if (wowSettingsElement != null)
                        {
                            profile.Settings.WowSettings.LoginData =
                                GetElementValue<string>(wowSettingsElement.Element("LoginData"));
                            profile.Settings.WowSettings.PasswordData =
                                GetElementValue<string>(wowSettingsElement.Element("PasswordData"));
                            profile.Settings.WowSettings.AcountName =
                                GetElementValue<string>(wowSettingsElement.Element("AcountName"));
                            profile.Settings.WowSettings.CharacterName =
                                GetElementValue<string>(wowSettingsElement.Element("CharacterName"));
                            profile.Settings.WowSettings.ServerName =
                                GetElementValue<string>(wowSettingsElement.Element("ServerName"));
                            profile.Settings.WowSettings.Region =
                                GetElementValue<WowSettings.WowRegion>(wowSettingsElement.Element("Region"));
                            profile.Settings.WowSettings.WowPath =
                                GetElementValue<string>(wowSettingsElement.Element("WowPath"));
                            profile.Settings.WowSettings.WowWindowWidth =
                                GetElementValue<int>(wowSettingsElement.Element("WowWindowWidth"));
                            profile.Settings.WowSettings.WowWindowHeight =
                                GetElementValue<int>(wowSettingsElement.Element("WowWindowHeight"));
                            profile.Settings.WowSettings.WowWindowX =
                                GetElementValue<int>(wowSettingsElement.Element("WowWindowX"));
                            profile.Settings.WowSettings.WowWindowY =
                                GetElementValue<int>(wowSettingsElement.Element("WowWindowY"));
                        }
                        XElement hbSettingsElement = settingsElement.Element("HonorbuddySettings");
                        // Honorbuddy Settings
                        if (hbSettingsElement != null)
                        {
                            profile.Settings.HonorbuddySettings.CustomClass =
                                GetElementValue<string>(hbSettingsElement.Element("CustomClass"));
                            profile.Settings.HonorbuddySettings.BotBase =
                                GetElementValue<string>(hbSettingsElement.Element("BotBase"));
                            profile.Settings.HonorbuddySettings.HonorbuddyProfile =
                                GetElementValue<string>(hbSettingsElement.Element("HonorbuddyProfile"));
                            profile.Settings.HonorbuddySettings.HonorbuddyPath =
                                GetElementValue<string>(hbSettingsElement.Element("HonorbuddyPath"));
                        }
                        XElement tasksElement = profileElement.Element("Tasks");
                        // Load the Task list.
                        foreach (XElement taskElement in tasksElement.Elements())
                        {
                            Type taskType = Type.GetType("HighVoltz.HBRelog.Tasks." + taskElement.Name);
                            if (taskType != null)
                            {
                                var task = (BMTask)Activator.CreateInstance(taskType);
                                task.SetProfile(profile);
                                // Dictionary of property Names and the corresponding PropertyInfo
                                Dictionary<string, PropertyInfo> propertyDict = task.GetType().GetProperties().
                                    Where(
                                        pi =>
                                        pi.GetCustomAttributesData().All(cad => cad.Constructor.DeclaringType != typeof(XmlIgnoreAttribute))).
                                    ToDictionary(k => k.Name);

                                foreach (XAttribute attr in taskElement.Attributes())
                                {
                                    string propKey = attr.Name.ToString();
                                    if (propertyDict.ContainsKey(propKey))
                                    {
                                        // if property is an enum then use Enum.Parse.. otherwise use Convert.ChangeValue
                                        object val = typeof(Enum).IsAssignableFrom(propertyDict[propKey].PropertyType) ?
                                            Enum.Parse(propertyDict[propKey].PropertyType, attr.Value) :
                                            Convert.ChangeType(attr.Value, propertyDict[propKey].PropertyType);
                                        propertyDict[propKey].SetValue(task, val, null);
                                    }
                                    else
                                    {
                                        Log.Err("{0} does not have a property called {1}", taskElement.Name, attr.Name);
                                    }
                                }
                                profile.Tasks.Add(task);
                            }
                            else
                            {
                                Log.Err("{0} is not a known task type", taskElement.Name);
                            }
                        }
                        settings.CharacterProfiles.Add(profile);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
            return settings;
        }

        private static T GetElementValue<T>(XElement element, T defaultValue = default(T))
        {
            if (element != null)
            {
                if (defaultValue is Enum)
                {
                    return (T)Enum.Parse(typeof(T), element.Value);
                }
                return (T)Convert.ChangeType(element.Value, typeof(T));
            }
            return defaultValue;
        }

        private Timer _autoSaveTimer;
        private DateTime _lastSaveTimeStamp;
        public void QueueSave()
        {
            if (DateTime.Now - _lastSaveTimeStamp >= TimeSpan.FromSeconds(5) && _autoSaveTimer == null)
                Save();
            else
            {
                if (_autoSaveTimer != null)
                    _autoSaveTimer.Dispose();

                _autoSaveTimer = new Timer(
                    state =>
                        {
                            Save();
                            _autoSaveTimer.Dispose();
                            _autoSaveTimer = null;
                        },
                    null,
                    5000,
                    -1);
            }
            _lastSaveTimeStamp = DateTime.Now;
        }

        public TimeSpan SaveCompleteTimeSpan
        {
            get
            {
                var timeSinceLastSave = DateTime.Now - _lastSaveTimeStamp;
                // check if a save queue is in process
                if (_autoSaveTimer != null && timeSinceLastSave < TimeSpan.FromSeconds(7))
                {
                    return TimeSpan.FromSeconds(7) - timeSinceLastSave;
                }
                else if (timeSinceLastSave < TimeSpan.FromSeconds(2))
                {
                    return TimeSpan.FromSeconds(2) - timeSinceLastSave;
                }
                return TimeSpan.FromSeconds(0);
            }
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