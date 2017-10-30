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
using System.Globalization;

namespace HighVoltz.HBRelog.Settings
{
    public class GlobalSettings : SettingsBase
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

        internal static string GetSettingsPath()
        {
            if (File.Exists(LocalSettingsPath))
                return LocalSettingsPath;

            if (File.Exists(Program.UserSettingsPath))
                return Program.UserSettingsPath;

            return LocalSettingsPath;
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

        // serializers giving me issues with colections.. so saving stuff manually.
        public void Save(string path = null)
        {
            try
            {
                SettingsPath = path ?? (UseLocalSettings ? LocalSettingsPath : Program.UserSettingsPath);
                var xml = ConvertToXml();
                
                var tempPath = GetTempSettingsPath(SettingsPath);
                var directory = Path.GetDirectoryName(tempPath);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var xmlSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, };
                using (var tempFile = ObtainLock(tempPath, FileAccess.Write, FileShare.Delete))
                {
                    using (XmlWriter xmlOutFile = XmlWriter.Create(tempFile, xmlSettings))
                        xml.Save(xmlOutFile);

                    if (File.Exists(SettingsPath))
                        File.Delete(SettingsPath);

                    File.Move(tempPath, SettingsPath);

                    // Maintain only one copy of settings.
                    if (UseLocalSettings && File.Exists(Program.UserSettingsPath))
                        File.Delete(Program.UserSettingsPath);
                    else if (!UseLocalSettings && File.Exists(LocalSettingsPath))
                        File.Delete(LocalSettingsPath);
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        public override XElement ConvertToXml()
        {
            var xml = new XElement("BotManager");
            xml.Add(new XElement("AutoStart", AutoStart));
            xml.Add(new XElement("WowDelay", WowDelay));
            xml.Add(new XElement("HBDelay", HBDelay));
            xml.Add(new XElement("LoginDelay", LoginDelay));
            xml.Add(new XElement("UseDarkStyle", UseDarkStyle));
            xml.Add(new XElement("CheckRealmStatus", CheckRealmStatus));
            xml.Add(new XElement("CheckHbResponsiveness", CheckHbResponsiveness));
            xml.Add(new XElement("CheckWowResponsiveness", CheckWowResponsiveness));
            xml.Add(new XElement("UseLocalSettings", UseLocalSettings));
            xml.Add(new XElement("AutoUpdateHB", AutoUpdateHB));
            xml.Add(new XElement("AutoAcceptTosEula", AutoAcceptTosEula));
            xml.Add(new XElement("SetGameWindowTitle", SetGameWindowTitle));
            xml.Add(new XElement("GameWindowTitle", GameWindowTitle));
            xml.Add(new XElement("EncryptSettings", EncryptSettings));
            xml.Add(new XElement("WowVersion", WowVersion));
            xml.Add(new XElement("GameStateOffset", GameStateOffset));
            xml.Add(new XElement("FocusedWidgetOffset", FocusedWidgetOffset));
            xml.Add(new XElement("LuaStateOffset", LuaStateOffset));
            xml.Add(new XElement("LoadingScreenEnableCountOffset", LoadingScreenEnableCountOffset));

            var characterProfilesElement = new XElement("CharacterProfiles");
            foreach (CharacterProfile profile in CharacterProfiles)
            {
                var profileElement = profile.ConvertToXml();
                characterProfilesElement.Add(profileElement);
            }
            xml.Add(characterProfilesElement);
            return xml;
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
        /// 
        private void Load(string path)
        {
            try
            {
                IsLoaded = false;
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
                    LoadFromXml(root);
                }

                SettingsPath = UseLocalSettings ? LocalSettingsPath : Program.UserSettingsPath;
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
            finally
            {
                IsLoaded = true;
            }
        }

        public override void LoadFromXml(XElement element)
        {
            WowVersion = element.Element("WowVersion").Value;
            AutoStart = GetElementValue<bool>(element.Element("AutoStart"));
            WowDelay = GetElementValue<int>(element.Element("WowDelay"));
            HBDelay = GetElementValue(element.Element("HBDelay"), 10);
            LoginDelay = GetElementValue(element.Element("LoginDelay"), 3);
            UseDarkStyle = GetElementValue(element.Element("UseDarkStyle"), true);
            CheckRealmStatus = GetElementValue(element.Element("CheckRealmStatus"), false);
            CheckHbResponsiveness = GetElementValue(element.Element("CheckHbResponsiveness"), true);
            CheckWowResponsiveness = GetElementValue(element.Element("CheckWowResponsiveness"), true);
            AutoUpdateHB = GetElementValue(element.Element("AutoUpdateHB"), true);
            UseLocalSettings = GetElementValue(element.Element("UseLocalSettings"), false);
            AutoAcceptTosEula = GetElementValue(element.Element("AutoAcceptTosEula"), false);
            SetGameWindowTitle = GetElementValue(element.Element("SetGameWindowTitle"), true);
            GameWindowTitle = GetElementValue(element.Element("GameWindowTitle"), "{name} - {pid}");
            EncryptSettings = GetElementValue(element.Element("EncryptSettings"), true);
            GameStateOffset = GetElementValue(element.Element("GameStateOffset"), 0u);
            // settings.FrameScriptExecuteOffset = GetElementValue(root.Element("FrameScriptExecuteOffset"), 0u);
            FocusedWidgetOffset = GetElementValue(element.Element("FocusedWidgetOffset"), 0u);
            LuaStateOffset = GetElementValue(element.Element("LuaStateOffset"), 0u);
            LoadingScreenEnableCountOffset = GetElementValue(element.Element("LoadingScreenEnableCountOffset"), 0u);
            //settings.LastHardwareEventOffset = GetElementValue(root.Element("LastHardwareEventOffset"), 0u);
            CharacterProfiles.Clear();
            XElement characterProfilesElement = element.Element("CharacterProfiles");
            foreach (XElement profileElement in characterProfilesElement.Elements("CharacterProfile"))
            {
                var profile = new CharacterProfile();
                profile.LoadFromXml(profileElement);
                CharacterProfiles.Add(profile);
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

        public override SettingsBase ShadowCopy()
        {
            var settings = (GlobalSettings)MemberwiseClone();
            settings.CharacterProfiles = new ObservableCollection<CharacterProfile>();
            foreach (var characterProfile in CharacterProfiles)
                settings.CharacterProfiles.Add(characterProfile.ShadowCopy());
            return settings;
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