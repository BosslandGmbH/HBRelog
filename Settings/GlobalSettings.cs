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
using System.Diagnostics;
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
		public static GlobalSettings Instance { get; private set; }

	    static GlobalSettings()
	    {
			Instance = new GlobalSettings();
			Instance.Load(GetSettingsPath());
	    }

		private bool _autoAcceptTosEula;
        private Timer _autoSaveTimer;
        private bool _autoStart;
        private bool _autoUpdateHB;
        private bool _checkHbResponsiveness;
        private bool _checkWowResponsiveness;
        private bool _checkRealmStatus;
		private string _gameWindowTitle;
		private int _hBDelay;
        private DateTime _lastSaveTimeStamp;
        private int _loginDelay;
        private bool _useLocalSettings;
        private bool _encryptSettings;
		private bool _useDarkStyle;
		private bool _setGameWindowTitle;
        private int _wowDelay;

        private GlobalSettings()
        {
            CharacterProfiles = new ObservableCollection<CharacterProfile>();
            // set some default settings
            HBDelay = 3;
            AutoUpdateHB = CheckHbResponsiveness = UseDarkStyle = true;
        }

        private static string LocalSettingsPath => Path.Combine(Utility.AssemblyDirectory, "Settings.xml");

        private static string UserSettingsPath
            =>
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HighVoltz\\HBRelog\\Settings.xml");

        private static string OldUserSettingsPath
            =>
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HighVoltz\\HBRelog\\Setting.xml");

        private static string GetSettingsPath()
        {
            if (File.Exists(LocalSettingsPath))
                return LocalSettingsPath;

            // Rename old file to new file name
            if (File.Exists(OldUserSettingsPath))
                File.Move(OldUserSettingsPath, UserSettingsPath);

            return UserSettingsPath;
        }

	    public string SettingsPath { get; private set; }


		private static string GetTempSettingsPath(string settingsPath)
	    {
		    return settingsPath + ".tmp";
	    }

        public ObservableCollection<CharacterProfile> CharacterProfiles { get; private set; }
        // Automatically start all enabled profiles on start

	    public bool AutoStart
	    {
		    get { return _autoStart; }
		    set { NotifyPropertyChanged(ref _autoStart, ref value, nameof(AutoStart)); }
	    }

        // delay in seconds between starting multiple Wow instance

	    public int WowDelay
	    {
		    get { return _wowDelay; }
		    set { NotifyPropertyChanged(ref _wowDelay, ref value, nameof(WowDelay)); }
	    }

	    public string GameWindowTitle
	    {
		    get { return _gameWindowTitle; }
		    set { NotifyPropertyChanged(ref _gameWindowTitle, ref value, nameof(GameWindowTitle)); }
	    }

        // delay in seconds between starting multiple Honorbuddy instance

        public int HBDelay
        {
            get { return _hBDelay; }
            set { NotifyPropertyChanged(ref _hBDelay, ref value, nameof(HBDelay)); }
        }

        // delay in seconds between executing login actions.

	    public int LoginDelay
	    {
		    get { return _loginDelay; }
		    set { NotifyPropertyChanged(ref _loginDelay, ref value, nameof(LoginDelay)); }
	    }

	    public bool UseDarkStyle
	    {
		    get { return _useDarkStyle; }
		    set { NotifyPropertyChanged(ref _useDarkStyle, ref value, nameof(UseDarkStyle)); }
	    }

	    public bool SetGameWindowTitle
	    {
		    get { return _setGameWindowTitle; }
		    set { NotifyPropertyChanged(ref _setGameWindowTitle, ref value, nameof(SetGameWindowTitle)); }
	    }

	    public bool CheckRealmStatus
	    {
		    get { return _checkRealmStatus; }
		    set { NotifyPropertyChanged(ref _checkRealmStatus, ref value, nameof(CheckRealmStatus)); }
	    }

	    public bool CheckHbResponsiveness
	    {
		    get { return _checkHbResponsiveness; }
		    set { NotifyPropertyChanged(ref _checkHbResponsiveness, ref value, nameof(CheckHbResponsiveness)); }
	    }

	    public bool CheckWowResponsiveness
	    {
		    get { return _checkWowResponsiveness; }
		    set { NotifyPropertyChanged(ref _checkWowResponsiveness, ref value, nameof(CheckWowResponsiveness)); }
	    }

	    public bool AutoUpdateHB
	    {
		    get { return _autoUpdateHB; }
		    set { NotifyPropertyChanged(ref _autoUpdateHB, ref value, nameof(AutoUpdateHB)); }
	    }

	    public bool AutoAcceptTosEula
	    {
		    get { return _autoAcceptTosEula; }
		    set { NotifyPropertyChanged(ref _autoAcceptTosEula, ref value, nameof(AutoAcceptTosEula)); }
	    }

        /// <summary>
        ///     Minimizes HB to system tray on start
        /// </summary>
        public bool UseLocalSettings
        {
	        get { return _useLocalSettings; }
	        set { NotifyPropertyChanged(ref _useLocalSettings, ref value, nameof(UseLocalSettings)); }
        }


	    public bool EncryptSettings
	    {
		    get { return _encryptSettings; }
		    set
		    {
			    if (value == _encryptSettings)
				    return;

			    if (value)
				    ConvertToEncryption();
			    else
				    ConvertToPlaintext();

			    NotifyPropertyChanged(ref _encryptSettings, ref value, nameof(EncryptSettings));
		    }
	    }

		public string WowVersion { get; set; }

        // offsets
        public uint GameStateOffset { get; set; }
        public uint FocusedWidgetOffset { get; set; }
        public uint LuaStateOffset { get; set; }
		public uint LoadingScreenEnableCountOffset { get; set; }

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
        // serializers giving me issues with colections.. so saving stuff manually.
        public void Save(string path = null)
        {
            try
            {
                SettingsPath = path ?? (UseLocalSettings ? LocalSettingsPath : UserSettingsPath);
                var root = new XElement("BotManager");
                root.Add(new XElement("AutoStart", AutoStart));
                root.Add(new XElement("WowDelay", WowDelay));
                root.Add(new XElement("HBDelay", HBDelay));
                root.Add(new XElement("LoginDelay", LoginDelay));
                root.Add(new XElement("UseDarkStyle", UseDarkStyle));
                root.Add(new XElement("CheckRealmStatus", CheckRealmStatus));
                root.Add(new XElement("CheckHbResponsiveness", CheckHbResponsiveness));
                root.Add(new XElement("CheckWowResponsiveness", CheckWowResponsiveness));
                root.Add(new XElement("UseLocalSettings", UseLocalSettings));
                root.Add(new XElement("AutoUpdateHB", AutoUpdateHB));
                root.Add(new XElement("AutoAcceptTosEula", AutoAcceptTosEula));
				root.Add(new XElement("SetGameWindowTitle", SetGameWindowTitle));
				root.Add(new XElement("GameWindowTitle", GameWindowTitle));
				root.Add(new XElement("EncryptSettings", EncryptSettings));

				root.Add(new XElement("WowVersion", WowVersion));

                root.Add(new XElement("GameStateOffset", GameStateOffset));
                root.Add(new XElement("FocusedWidgetOffset", FocusedWidgetOffset));
                root.Add(new XElement("LuaStateOffset", LuaStateOffset));
				root.Add(new XElement("LoadingScreenEnableCountOffset", LoadingScreenEnableCountOffset));

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
					wowSettingsElement.Add(new XElement("AuthenticatorSerialData", profile.Settings.WowSettings.AuthenticatorSerialData));
					wowSettingsElement.Add(new XElement("AuthenticatorRestoreCodeData", profile.Settings.WowSettings.AuthenticatorRestoreCodeData));
                    wowSettingsElement.Add(new XElement("Region", profile.Settings.WowSettings.Region));
                    wowSettingsElement.Add(new XElement("WowPath", profile.Settings.WowSettings.WowPath));
                    wowSettingsElement.Add(new XElement("WowArgs", profile.Settings.WowSettings.WowArgs));
                    wowSettingsElement.Add(new XElement("WowWindowWidth", profile.Settings.WowSettings.WowWindowWidth));
                    wowSettingsElement.Add(new XElement("WowWindowHeight", profile.Settings.WowSettings.WowWindowHeight));
                    wowSettingsElement.Add(new XElement("WowWindowX", profile.Settings.WowSettings.WowWindowX));
                    wowSettingsElement.Add(new XElement("WowWindowY", profile.Settings.WowSettings.WowWindowY));
                    settingsElement.Add(wowSettingsElement);
                    var hbSettingsElement = new XElement("HonorbuddySettings");
                    // Honorbuddy Settings
                    hbSettingsElement.Add(new XElement("HonorbuddyKeyData", profile.Settings.HonorbuddySettings.HonorbuddyKeyData));
                    hbSettingsElement.Add(new XElement("CustomClass", profile.Settings.HonorbuddySettings.CustomClass));
                    hbSettingsElement.Add(new XElement("HonorbuddyArgs", profile.Settings.HonorbuddySettings.HonorbuddyArgs));
					hbSettingsElement.Add(new XElement("BotBase", profile.Settings.HonorbuddySettings.BotBase));
                    hbSettingsElement.Add(new XElement("HonorbuddyProfile", profile.Settings.HonorbuddySettings.HonorbuddyProfile));
                    hbSettingsElement.Add(new XElement("HonorbuddyPath", profile.Settings.HonorbuddySettings.HonorbuddyPath));
                    hbSettingsElement.Add(new XElement("UseHBBeta", profile.Settings.HonorbuddySettings.UseHBBeta));

                    settingsElement.Add(hbSettingsElement);
                    profileElement.Add(settingsElement);
                    var tasksElement = new XElement("Tasks");

                    foreach (BMTask task in profile.Tasks)
                    {
                        var taskElement = new XElement(task.GetType().Name);
                        // get a list of propertyes that don't have [XmlIgnore] custom attribute attached.
                        List<PropertyInfo> propertyList =
                            task.GetType()
                                .GetProperties()
                                .Where(pi => pi.GetCustomAttributesData().All(cad => cad.Constructor.DeclaringType != typeof(XmlIgnoreAttribute)))
                                .ToList();
                        foreach (PropertyInfo property in propertyList)
                        {
	                        var value = property.GetValue(task, null);
							Debug.Assert(value != null, string.Format("value for {0} != null", property.Name));
							taskElement.Add(new XAttribute(property.Name, value));
                        }
                        tasksElement.Add(taskElement);
                    }
                    profileElement.Add(tasksElement);
                    characterProfilesElement.Add(profileElement);
                }
                root.Add(characterProfilesElement);


                var tempPath = GetTempSettingsPath(SettingsPath);
				var directory = Path.GetDirectoryName(tempPath);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var xmlSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, };
				using (var tempFile = ObtainLock(tempPath, FileAccess.Write, FileShare.Delete))
                {
                    using (XmlWriter xmlOutFile = XmlWriter.Create(tempFile, xmlSettings))
                        root.Save(xmlOutFile);

                    if (File.Exists(SettingsPath))
                        File.Delete(SettingsPath);

					File.Move(tempPath, SettingsPath);

                    // Maintain only one copy of settings.
                    if (UseLocalSettings && File.Exists(UserSettingsPath))
                        File.Delete(UserSettingsPath);
                    else if (!UseLocalSettings && File.Exists(LocalSettingsPath))
                        File.Delete(LocalSettingsPath);
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        private static FileStream ObtainLock(string path, FileAccess access, FileShare share = FileShare.None, int maxWaitTimeMs = 500)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    return File.Open(path, FileMode.OpenOrCreate, access, share);
                }
                catch (Exception)
                {
                    if (sw.ElapsedMilliseconds >= maxWaitTimeMs)
                        throw;
                }
                Thread.Sleep(0);
            }
        }

        /// <summary>
        ///     Attempts to load settings from file
        /// </summary>
        /// <returns>A GlocalSettings</returns>
        private void Load(string path)
        {
            try
            {
				var hasSettings = File.Exists(path);

				var tempPath = GetTempSettingsPath(path);

				var recoverFromCrash = !hasSettings && File.Exists(tempPath);
                if (hasSettings || recoverFromCrash)
                {
                    if (recoverFromCrash)
                    {
						File.Move(tempPath, path);
                    }

					XElement root = XElement.Load(path);
                    WowVersion = root.Element("WowVersion").Value;
                    AutoStart = GetElementValue<bool>(root.Element("AutoStart"));
                    WowDelay = GetElementValue<int>(root.Element("WowDelay"));
                    HBDelay = GetElementValue(root.Element("HBDelay"), 10);
                    LoginDelay = GetElementValue(root.Element("LoginDelay"), 3);
                    UseDarkStyle = GetElementValue(root.Element("UseDarkStyle"), true);
                    CheckRealmStatus = GetElementValue(root.Element("CheckRealmStatus"), false);
                    CheckHbResponsiveness = GetElementValue(root.Element("CheckHbResponsiveness"), true);
                    CheckWowResponsiveness = GetElementValue(root.Element("CheckWowResponsiveness"), true);
                    AutoUpdateHB = GetElementValue(root.Element("AutoUpdateHB"), true);
                    UseLocalSettings = GetElementValue(root.Element("UseLocalSettings"), false);
                    AutoAcceptTosEula = GetElementValue(root.Element("AutoAcceptTosEula"), false);
					SetGameWindowTitle = GetElementValue(root.Element("SetGameWindowTitle"), true);
					GameWindowTitle = GetElementValue(root.Element("GameWindowTitle"), "{name} - {pid}");
					EncryptSettings = GetElementValue(root.Element("EncryptSettings"), true);

					GameStateOffset = GetElementValue(root.Element("GameStateOffset"), 0u);
                    // settings.FrameScriptExecuteOffset = GetElementValue(root.Element("FrameScriptExecuteOffset"), 0u);
                    FocusedWidgetOffset = GetElementValue(root.Element("FocusedWidgetOffset"), 0u);
                    LuaStateOffset = GetElementValue(root.Element("LuaStateOffset"), 0u);
                    LoadingScreenEnableCountOffset = GetElementValue(root.Element("LoadingScreenEnableCountOffset"), 0u);

                    //settings.LastHardwareEventOffset = GetElementValue(root.Element("LastHardwareEventOffset"), 0u);

                    CharacterProfiles.Clear();
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
                            profile.Settings.WowSettings.LoginData = GetElementValue<string>(wowSettingsElement.Element("LoginData"));
                            profile.Settings.WowSettings.PasswordData = GetElementValue<string>(wowSettingsElement.Element("PasswordData"));
                            profile.Settings.WowSettings.AcountName = GetElementValue<string>(wowSettingsElement.Element("AcountName"));
                            profile.Settings.WowSettings.CharacterName = GetElementValue<string>(wowSettingsElement.Element("CharacterName"));
                            profile.Settings.WowSettings.ServerName = GetElementValue<string>(wowSettingsElement.Element("ServerName"));
							profile.Settings.WowSettings.AuthenticatorSerialData = GetElementValue<string>(wowSettingsElement.Element("AuthenticatorSerialData"));
							profile.Settings.WowSettings.AuthenticatorRestoreCodeData = GetElementValue<string>(wowSettingsElement.Element("AuthenticatorRestoreCodeData"));
                            profile.Settings.WowSettings.Region = GetElementValue<WowSettings.WowRegion>(wowSettingsElement.Element("Region"));
                            profile.Settings.WowSettings.WowPath = GetElementValue<string>(wowSettingsElement.Element("WowPath"));
                            profile.Settings.WowSettings.WowArgs = GetElementValue<string>(wowSettingsElement.Element("WowArgs"));
                            profile.Settings.WowSettings.WowWindowWidth = GetElementValue<int>(wowSettingsElement.Element("WowWindowWidth"));
                            profile.Settings.WowSettings.WowWindowHeight = GetElementValue<int>(wowSettingsElement.Element("WowWindowHeight"));
                            profile.Settings.WowSettings.WowWindowX = GetElementValue<int>(wowSettingsElement.Element("WowWindowX"));
                            profile.Settings.WowSettings.WowWindowY = GetElementValue<int>(wowSettingsElement.Element("WowWindowY"));
                        }
                        XElement hbSettingsElement = settingsElement.Element("HonorbuddySettings");
                        // Honorbuddy Settings
                        if (hbSettingsElement != null)
                        {
                            profile.Settings.HonorbuddySettings.HonorbuddyKeyData = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyKeyData"));
                            profile.Settings.HonorbuddySettings.CustomClass = GetElementValue<string>(hbSettingsElement.Element("CustomClass"));
                            profile.Settings.HonorbuddySettings.HonorbuddyArgs = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyArgs"));
							profile.Settings.HonorbuddySettings.BotBase = GetElementValue<string>(hbSettingsElement.Element("BotBase"));
                            profile.Settings.HonorbuddySettings.HonorbuddyProfile = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyProfile"));
                            profile.Settings.HonorbuddySettings.HonorbuddyPath = GetElementValue<string>(hbSettingsElement.Element("HonorbuddyPath"));
                            profile.Settings.HonorbuddySettings.UseHBBeta = GetElementValue<bool>(hbSettingsElement.Element("UseHBBeta"));
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
                                Dictionary<string, PropertyInfo> propertyDict =
                                    task.GetType()
                                        .GetProperties()
                                        .Where(pi => pi.GetCustomAttributesData().All(cad => cad.Constructor.DeclaringType != typeof(XmlIgnoreAttribute)))
                                        .ToDictionary(k => k.Name);

                                foreach (XAttribute attr in taskElement.Attributes())
                                {
                                    string propKey = attr.Name.ToString();
                                    if (propertyDict.ContainsKey(propKey))
                                    {
                                        // if property is an enum then use Enum.Parse.. otherwise use Convert.ChangeValue
                                        object val = typeof(Enum).IsAssignableFrom(propertyDict[propKey].PropertyType)
                                                         ? Enum.Parse(propertyDict[propKey].PropertyType, attr.Value)
                                                         : Convert.ChangeType(attr.Value, propertyDict[propKey].PropertyType);
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
                        CharacterProfiles.Add(profile);
                    }
                }

                SettingsPath = UseLocalSettings ? LocalSettingsPath : UserSettingsPath;
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        static readonly byte[] Key = { 230, 123, 245, 78, 43, 229, 126, 109, 126, 10, 134, 61, 167, 2, 138, 142 };
        static readonly byte[] Iv = { 113, 110, 177, 211, 193, 101, 36, 36, 52, 12, 51, 73, 61, 42, 239, 236 };

        public void Import(string path)
        {
			Load(path);
            foreach (var characterProfile in CharacterProfiles)
            {
	            var plainText = Utility.DecryptAes(characterProfile.Settings.WowSettings.LoginData, Key, Iv);
				characterProfile.Settings.WowSettings.LoginData = EncryptSettings
		            ? Utility.EncrptDpapi(plainText)
		            : plainText;

				plainText = Utility.DecryptAes(characterProfile.Settings.WowSettings.PasswordData, Key, Iv);
				characterProfile.Settings.WowSettings.PasswordData = EncryptSettings
					? Utility.EncrptDpapi(plainText)
					: plainText;

				plainText = Utility.DecryptAes(characterProfile.Settings.WowSettings.AuthenticatorSerialData, Key, Iv);
				characterProfile.Settings.WowSettings.AuthenticatorSerialData = EncryptSettings
					? Utility.EncrptDpapi(plainText)
					: plainText;

				plainText = Utility.DecryptAes(characterProfile.Settings.WowSettings.AuthenticatorRestoreCodeData, Key, Iv);
				characterProfile.Settings.WowSettings.AuthenticatorRestoreCodeData = EncryptSettings
					? Utility.EncrptDpapi(plainText)
					: plainText;

				plainText = Utility.DecryptAes(characterProfile.Settings.HonorbuddySettings.HonorbuddyKeyData, Key, Iv);
				characterProfile.Settings.HonorbuddySettings.HonorbuddyKeyData = EncryptSettings
					? Utility.EncrptDpapi(plainText)
					: plainText;
            }
        }

        public GlobalSettings Export(string path)
        {
            var settings = (GlobalSettings)MemberwiseClone();
            settings.CharacterProfiles = new ObservableCollection<CharacterProfile>();
            foreach (var characterProfile in CharacterProfiles)
            {
                var newProfile = characterProfile.ShadowCopy();

				var data = EncryptSettings
					? Utility.DecrptDpapi(characterProfile.Settings.WowSettings.LoginData)
					: characterProfile.Settings.WowSettings.LoginData;
				newProfile.Settings.WowSettings.LoginData = Utility.EncryptAes(data, Key, Iv);

	            data = EncryptSettings
		            ? Utility.DecrptDpapi(characterProfile.Settings.WowSettings.PasswordData)
		            : characterProfile.Settings.WowSettings.PasswordData;
                newProfile.Settings.WowSettings.PasswordData = Utility.EncryptAes(data, Key, Iv);

				data = EncryptSettings
					? Utility.DecrptDpapi(characterProfile.Settings.WowSettings.AuthenticatorSerialData)
					: characterProfile.Settings.WowSettings.AuthenticatorSerialData;
				newProfile.Settings.WowSettings.AuthenticatorSerialData = Utility.EncryptAes(data, Key, Iv);

				data = EncryptSettings
					? Utility.DecrptDpapi(characterProfile.Settings.WowSettings.AuthenticatorRestoreCodeData)
					: characterProfile.Settings.WowSettings.AuthenticatorRestoreCodeData;
				newProfile.Settings.WowSettings.AuthenticatorRestoreCodeData = Utility.EncryptAes(data, Key, Iv);

				data = EncryptSettings
					? Utility.DecrptDpapi(characterProfile.Settings.HonorbuddySettings.HonorbuddyKeyData)
					: characterProfile.Settings.HonorbuddySettings.HonorbuddyKeyData;
				newProfile.Settings.HonorbuddySettings.HonorbuddyKeyData = Utility.EncryptAes(data, Key, Iv);

                settings.CharacterProfiles.Add(newProfile);
            }
	        settings.SettingsPath = path;
            return settings;
        }

        public GlobalSettings ShadowCopy()
        {
            var settings = (GlobalSettings)MemberwiseClone();
            settings.CharacterProfiles = new ObservableCollection<CharacterProfile>();
            foreach (var characterProfile in CharacterProfiles)
            {
                settings.CharacterProfiles.Add(characterProfile.ShadowCopy());
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

        public void QueueSave()
        {
            if (DateTime.Now - _lastSaveTimeStamp >= TimeSpan.FromSeconds(5) && _autoSaveTimer == null)
            {
	            Save();
	            return;
            }
	        _autoSaveTimer?.Dispose();
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
	        _lastSaveTimeStamp = DateTime.Now;
        }

        private bool NotifyPropertyChanged<T>(ref T oldValue, ref T newValue, string propertyName)
        {
	        if (Equals(oldValue, newValue))
		        return false;
	        oldValue = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	        if (HbRelogManager.Settings != null)
                HbRelogManager.Settings.QueueSave();
	        return true;
        }

	    private void ConvertToEncryption()
	    {
			foreach (var characterProfile in CharacterProfiles)
			{
				var settings = characterProfile.Settings;
				settings.WowSettings.LoginData = Utility.EncrptDpapi(settings.WowSettings.LoginData);
				settings.WowSettings.PasswordData = Utility.EncrptDpapi(settings.WowSettings.PasswordData);
				settings.WowSettings.AuthenticatorSerialData = Utility.EncrptDpapi(settings.WowSettings.AuthenticatorSerialData);
				settings.WowSettings.AuthenticatorRestoreCodeData = Utility.EncrptDpapi(settings.WowSettings.AuthenticatorRestoreCodeData);
				settings.HonorbuddySettings.HonorbuddyKeyData = Utility.EncrptDpapi(settings.HonorbuddySettings.HonorbuddyKeyData);
			}
		}

		private void ConvertToPlaintext()
		{
			foreach (var characterProfile in CharacterProfiles)
			{
				var settings = characterProfile.Settings;
				settings.WowSettings.LoginData = Utility.DecrptDpapi(settings.WowSettings.LoginData);
				settings.WowSettings.PasswordData = Utility.DecrptDpapi(settings.WowSettings.PasswordData);
				settings.WowSettings.AuthenticatorSerialData = Utility.DecrptDpapi(settings.WowSettings.AuthenticatorSerialData);
				settings.WowSettings.AuthenticatorRestoreCodeData = Utility.DecrptDpapi(settings.WowSettings.AuthenticatorRestoreCodeData);

				// In the past, HonorbuddyKeyData was not encrypted when it represented an empty string 
				if (settings.HonorbuddySettings.HonorbuddyKeyData != "")
					settings.HonorbuddySettings.HonorbuddyKeyData = Utility.DecrptDpapi(settings.HonorbuddySettings.HonorbuddyKeyData);
			}
		}
	}
}