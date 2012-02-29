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
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Linq;
using System.Runtime.Serialization;
using System.IO.IsolatedStorage;
using HighVoltz.HBRelog.Tasks;


namespace HighVoltz.HBRelog.Settings
{
    public class GlobalSettings
    {
        private GlobalSettings()
        {
            CharacterProfiles = new ObservableCollection<CharacterProfile>();
            string settingsFolder = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(settingsFolder))
                Directory.CreateDirectory(settingsFolder);
        }
        public string SettingsPath { 
            get 
            { 
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + 
                    "\\HighVoltz\\HBRelog\\Setting.xml"; 
            } 
        }
        public ObservableCollection<CharacterProfile> CharacterProfiles { get; set; }
        // Automatically start all enabled profiles on start
        public bool AutoStart { get; set; }
        // delay in seconds between starting multiple Wow instance
        public int WowDelay { get; set; }
        // delay in seconds between starting multiple Honorbuddy instance
        public int HBDelay { get; set; }
        // delay in seconds between executing login actions.
        public int LoginDelay { get; set; }
        public bool UseDarkStyle { get; set; }
        public bool CheckRealmStatus { get; set; }

        public string WowVersion { get; set; }
        // offsets
        public uint DxDeviceOffset { get; set; }
        public uint DxDeviceIndex { get; set; }
        public uint GameStateOffset { get; set; }
        public uint FrameScriptExecuteOffset { get; set; }
        public uint LastHardwareEventOffset { get; set; }
        public uint GlueStateOffset { get; set; }
        // serializers giving me issues with colections.. so saving stuff manually.
        public void Save()
        {
            try
            {
                XElement root = new XElement("BotManager");
                root.Add(new XElement("AutoStart", AutoStart));
                root.Add(new XElement("WowDelay", WowDelay));
                root.Add(new XElement("HBDelay", HBDelay));
                root.Add(new XElement("LoginDelay", LoginDelay));
                root.Add(new XElement("UseDarkStyle", UseDarkStyle));
                root.Add(new XElement("CheckRealmStatus", CheckRealmStatus));
                root.Add(new XElement("WowVersion", WowVersion));

                root.Add(new XElement("DxDeviceOffset", DxDeviceOffset));
                root.Add(new XElement("DxDeviceIndex", DxDeviceIndex));
                root.Add(new XElement("GameStateOffset", GameStateOffset));
                root.Add(new XElement("FrameScriptExecuteOffset", FrameScriptExecuteOffset));
                root.Add(new XElement("LastHardwareEventOffset", LastHardwareEventOffset));
                root.Add(new XElement("GlueStateOffset", GlueStateOffset));

                XElement characterProfilesElement = new XElement("CharacterProfiles");
                foreach (var profile in CharacterProfiles)
                {
                    XElement profileElement = new XElement("CharacterProfile");
                    XElement settingsElement = new XElement("Settings");
                    settingsElement.Add(new XElement("ProfileName", profile.Settings.ProfileName));
                    settingsElement.Add(new XElement("IsEnabled", profile.Settings.IsEnabled));
                    XElement wowSettingsElement = new XElement("WowSettings");
                    // Wow Settings 
                    wowSettingsElement.Add(new XElement("LoginData", profile.Settings.WowSettings.LoginData));
                    wowSettingsElement.Add(new XElement("PasswordData", profile.Settings.WowSettings.PasswordData));
                    wowSettingsElement.Add(new XElement("AcountName", profile.Settings.WowSettings.AcountName));
                    wowSettingsElement.Add(new XElement("CharacterName", profile.Settings.WowSettings.CharacterName));
                    wowSettingsElement.Add(new XElement("ServerName", profile.Settings.WowSettings.ServerName));
                    wowSettingsElement.Add(new XElement("WowPath", profile.Settings.WowSettings.WowPath));
                    wowSettingsElement.Add(new XElement("WowWindowWidth", profile.Settings.WowSettings.WowWindowWidth));
                    wowSettingsElement.Add(new XElement("WowWindowHeight", profile.Settings.WowSettings.WowWindowHeight));
                    wowSettingsElement.Add(new XElement("WowWindowX", profile.Settings.WowSettings.WowWindowX));
                    wowSettingsElement.Add(new XElement("WowWindowY", profile.Settings.WowSettings.WowWindowY));
                    settingsElement.Add(wowSettingsElement);
                    XElement hbSettingsElement = new XElement("HonorbuddySettings");
                    // Honorbuddy Settings
                    hbSettingsElement.Add(new XElement("CustomClass", profile.Settings.HonorbuddySettings.CustomClass));
                    hbSettingsElement.Add(new XElement("BotBase", profile.Settings.HonorbuddySettings.BotBase));
                    hbSettingsElement.Add(new XElement("HonorbuddyProfile", profile.Settings.HonorbuddySettings.HonorbuddyProfile));
                    hbSettingsElement.Add(new XElement("HonorbuddyPath", profile.Settings.HonorbuddySettings.HonorbuddyPath));
                    settingsElement.Add(hbSettingsElement);
                    profileElement.Add(settingsElement);
                    XElement tasksElement = new XElement("Tasks");

                    foreach (BMTask task in profile.Tasks)
                    {
                        XElement taskElement = new XElement(task.GetType().Name);
                        // get a list of propertyes that don't have [XmlIgnore] custom attribute attached.
                        List<PropertyInfo> propertyList = task.GetType().GetProperties().
                                        Where(pi => !pi.GetCustomAttributesData().Any(cad => cad.Constructor.DeclaringType == typeof(XmlIgnoreAttribute))).ToList();
                        foreach (var property in propertyList)
                        {
                            taskElement.Add(new XAttribute(property.Name, property.GetValue(task, null)));
                        }
                        tasksElement.Add(taskElement);
                    }
                    profileElement.Add(tasksElement);
                    characterProfilesElement.Add(profileElement);
                }
                root.Add(characterProfilesElement);
                root.Save(SettingsPath);
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }
        /// <summary>
        /// Attempts to load settings from file
        /// </summary>
        /// <param name="file"></param>
        /// <returns>A GlocalSettings</returns>
        public static GlobalSettings Load()
        { 
            GlobalSettings settings = new GlobalSettings();
            if (File.Exists(settings.SettingsPath))
            {
                XElement root = XElement.Load(settings.SettingsPath);
                settings.WowVersion = root.Element("WowVersion").Value;
                settings.AutoStart = GetElementValue<bool>(root.Element("AutoStart"));
                settings.WowDelay = GetElementValue<int>(root.Element("WowDelay"));
                settings.HBDelay = GetElementValue<int>(root.Element("HBDelay"),10);
                settings.LoginDelay = GetElementValue<int>(root.Element("LoginDelay"),3);
                settings.UseDarkStyle = GetElementValue<bool>(root.Element("UseDarkStyle"),true);
                settings.CheckRealmStatus = GetElementValue<bool>(root.Element("CheckRealmStatus"), true); 
                
                settings.DxDeviceOffset = uint.Parse(root.Element("DxDeviceOffset").Value);
                settings.DxDeviceIndex = uint.Parse(root.Element("DxDeviceIndex").Value);
                settings.GameStateOffset = uint.Parse(root.Element("GameStateOffset").Value);
                settings.FrameScriptExecuteOffset = uint.Parse(root.Element("FrameScriptExecuteOffset").Value);
                settings.LastHardwareEventOffset = uint.Parse(root.Element("LastHardwareEventOffset").Value);
                settings.GlueStateOffset = uint.Parse(root.Element("GlueStateOffset").Value);
                XElement characterProfilesElement = root.Element("CharacterProfiles");
                foreach (XElement profileElement in characterProfilesElement.Elements("CharacterProfile"))
                {
                    CharacterProfile profile = new CharacterProfile();
                    XElement settingsElement = profileElement.Element("Settings");
                    profile.Settings.ProfileName = GetElementValue<string>(settingsElement.Element("ProfileName"));
                    profile.Settings.IsEnabled = GetElementValue<bool>(settingsElement.Element("IsEnabled"));
                    XElement wowSettingsElement = settingsElement.Element("WowSettings");

                    // Wow Settings 
                    if (wowSettingsElement != null)
                    {
                        profile.Settings.WowSettings.LoginData = GetElementValue<string>(wowSettingsElement.Element("LoginData"));
                        profile.Settings.WowSettings.PasswordData = GetElementValue<string>(wowSettingsElement.Element("PasswordData"));
                        profile.Settings.WowSettings.AcountName = GetElementValue<string>(wowSettingsElement.Element("AcountName"));
                        profile.Settings.WowSettings.CharacterName = GetElementValue<string>(wowSettingsElement.Element("CharacterName"));
                        profile.Settings.WowSettings.ServerName = GetElementValue<string>(wowSettingsElement.Element("ServerName"));
                        profile.Settings.WowSettings.WowPath = GetElementValue<string>(wowSettingsElement.Element("WowPath"));
                        profile.Settings.WowSettings.WowWindowWidth = GetElementValue<int>(wowSettingsElement.Element("WowWindowWidth"));
                        profile.Settings.WowSettings.WowWindowHeight = GetElementValue<int>(wowSettingsElement.Element("WowWindowHeight"));
                        profile.Settings.WowSettings.WowWindowX = GetElementValue<int>(wowSettingsElement.Element("WowWindowX"));
                        profile.Settings.WowSettings.WowWindowY = GetElementValue<int>(wowSettingsElement.Element("WowWindowY"));
                    }
                    XElement hbSettingsElement = settingsElement.Element("HonorbuddySettings");
                    // Honorbuddy Settings
                    if (hbSettingsElement != null)
                    {
                        profile.Settings.HonorbuddySettings.CustomClass = GetElementValue<string>(hbSettingsElement.Element("CustomClass"));
                        profile.Settings.HonorbuddySettings.BotBase = GetElementValue<string>(hbSettingsElement.Element("BotBase"));
                        profile.Settings.HonorbuddySettings.HonorbuddyProfile = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyProfile"));
                        profile.Settings.HonorbuddySettings.HonorbuddyPath = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyPath"));
                    }
                    XElement tasksElement = profileElement.Element("Tasks");
                    // Load the Task list.
                    foreach (XElement taskElement in tasksElement.Elements())
                    {
                        Type taskType = Type.GetType("HighVoltz.HBRelog.Tasks." + taskElement.Name);
                        if (taskType != null)
                        {
                            BMTask task = (BMTask)Activator.CreateInstance(taskType);
                            task.SetProfile(profile);
                            // Dictionary of property Names and the corresponding PropertyInfo
                            Dictionary<string, PropertyInfo> propertyDict = task.GetType().GetProperties().
                                Where(pi => !pi.GetCustomAttributesData().Any(cad => cad.Constructor.DeclaringType == typeof(XmlIgnoreAttribute))).
                                ToDictionary(k => k.Name);

                            foreach (var attr in taskElement.Attributes())
                            {
                                string propKey = attr.Name.ToString();
                                if (propertyDict.ContainsKey(propKey))
                                {
                                    object val; // if property is an enum then use Enum.Parse.. otherwise use Convert.ChangeValue
                                    if (typeof(Enum).IsAssignableFrom(propertyDict[propKey].PropertyType))
                                        val = Enum.Parse(propertyDict[propKey].PropertyType, attr.Value);
                                    else
                                        val = Convert.ChangeType(attr.Value, propertyDict[propKey].PropertyType);
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
            return settings;
        }

        static T GetElementValue<T>(XElement element, T defaultValue = default(T))
        {
            if (element != null)
                return (T)Convert.ChangeType(element.Value, typeof(T));
            else
                return defaultValue;
        }
    }
}