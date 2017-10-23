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
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Xml.Linq;

namespace HighVoltz.HBRelog.Settings
{
    public class WowSettings : SettingsBase
    {
        public WowSettings()
        {
            Login = "Email@battle.net";
            Password = ServerName = AuthenticatorSerial = AuthenticatorRestoreCode = "";
            WowPath = string.Empty;
            WowArgs = string.Empty;
            AcountName = "WoW1";
            Region = WowRegion.Auto;
        }

        private string _loginData;
        public string LoginData { get { return _loginData; } set { _loginData = value; } }

        /// <summary>
        /// The Battlenet email address
        /// </summary>
        public string Login
        {
            get
            {
                try
                {
                    return GlobalSettings.Instance.EncryptSettings
                        ? Utility.DecrptDpapi(LoginData)
                        : LoginData;
                }
                catch
                {
                    // this error can occur if the Windows password was changed or profile was copied to another computer
                    MessageBox.Show($"Error decrypting login for {CharacterName}. Try setting login again.");
                    return "";
                }
            }
            set
            {
                string val = GlobalSettings.Instance.EncryptSettings ? Utility.EncrptDpapi(value) : value;
                NotifyPropertyChanged(ref _loginData, ref val, nameof(Login));
            }
        }


        private string _passwordData;
        public string PasswordData { get { return _passwordData; } set { _passwordData = value; } }

        public string Password
        {
            get
            {
                try
                {
                    if (!GlobalSettings.Instance.EncryptSettings)
                        return PasswordData;

                    var pass = Utility.DecrptDpapi(PasswordData);
                    return Utility.DecrptDpapi(PasswordData).Substring(0, Math.Min(16, pass.Length));
                }
                catch
                {
                    MessageBox.Show($"Error decrypting password for {CharacterName}. Try setting password again.");
                    return "";
                }
            }
            set
            {
                string val = GlobalSettings.Instance.EncryptSettings ? Utility.EncrptDpapi(value) : value;
                NotifyPropertyChanged(ref _passwordData, ref val, nameof(Password));
            }
        }

        private string _acountName;

        /// <summary>
        /// The name of the wow account. Only used if the battlenet acount has 2+ acounts attached.
        /// </summary>
        public string AcountName
        {
            get { return _acountName; }
            set { NotifyPropertyChanged(ref _acountName, ref value, nameof(AcountName)); }
        }

        private string _authenticatorRestoreCodeData;

        public string AuthenticatorRestoreCodeData
        {
            get { return _authenticatorRestoreCodeData; }
            set { _authenticatorRestoreCodeData = value; }
        }

        public string AuthenticatorRestoreCode
        {
            get
            {
                if (string.IsNullOrEmpty(AuthenticatorRestoreCodeData))
                    return "";
                try
                {
                    return GlobalSettings.Instance.EncryptSettings
                        ? Utility.DecrptDpapi(AuthenticatorRestoreCodeData)
                        : AuthenticatorRestoreCodeData;
                }
                catch
                {
                    MessageBox.Show(
                        $"Error decrypting Authenticator Restore code for {CharacterName}. Try setting the restore code again.");
                    return "";
                }
            }
            set
            {
                string val = GlobalSettings.Instance.EncryptSettings ? Utility.EncrptDpapi(value) : value;
                NotifyPropertyChanged(ref _authenticatorRestoreCodeData, ref val,
                                      nameof(AuthenticatorRestoreCode));
            }
        }

        private string _authenticatorSerialData;

        public string AuthenticatorSerialData
        {
            get { return _authenticatorSerialData; }
            set { _authenticatorSerialData = value; }
        }

        public string AuthenticatorSerial
        {
            get
            {
                if (string.IsNullOrEmpty(AuthenticatorSerialData))
                    return "";
                try
                {
                    return GlobalSettings.Instance.EncryptSettings
                        ? Utility.DecrptDpapi(AuthenticatorSerialData)
                        : AuthenticatorSerialData;
                }
                catch
                {
                    MessageBox.Show(
                        $"Error decrypting Authenticator Serial for {CharacterName}. Try setting the serial again.");
                    return "";
                }
            }
            set
            {
                string val = GlobalSettings.Instance.EncryptSettings ? Utility.EncrptDpapi(value) : value;
                NotifyPropertyChanged(ref _authenticatorSerialData, ref val, nameof(AuthenticatorSerial));
            }
        }

        private string _characterName;

        /// <summary>
        /// The in-game character name
        /// </summary>
        public string CharacterName
        {
            get { return _characterName; }
            set { NotifyPropertyChanged(ref _characterName, ref value, nameof(CharacterName)); }
        }

        private string _serverName;

        /// <summary>
        /// Name of the WoW server
        /// </summary>
        public string ServerName
        {
            get { return _serverName; }
            set { NotifyPropertyChanged(ref _serverName, ref value, nameof(ServerName)); }
        }

        private string _wowPath;

        /// <summary>
        /// Path to your WoW.Exe
        /// </summary>
        public string WowPath
        {
            get { return _wowPath; }
            set { NotifyPropertyChanged(ref _wowPath, ref value, nameof(WowPath)); }
        }

        private string _wowArgs;

        /// <summary>
        /// Command-line arguments to pass to WoW or launcher (Advanced)
        /// </summary>
        public string WowArgs
        {
            get { return _wowArgs; }
            set { NotifyPropertyChanged(ref _wowArgs, ref value, nameof(WowArgs)); }
        }

        private int _wowWindowWidth;

        public int WowWindowWidth
        {
            get { return _wowWindowWidth; }
            set { NotifyPropertyChanged(ref _wowWindowWidth, ref value, nameof(WowWindowWidth)); }
        }

        private int _wowWindowHeight;

        public int WowWindowHeight
        {
            get { return _wowWindowHeight; }
            set { NotifyPropertyChanged(ref _wowWindowHeight, ref value, nameof(WowWindowHeight)); }
        }

        private int _wowWindowX;

        public int WowWindowX
        {
            get { return _wowWindowX; }
            set { NotifyPropertyChanged(ref _wowWindowX, ref value, nameof(WowWindowX)); }
        }

        private int _wowWindowY;

        public int WowWindowY
        {
            get { return _wowWindowY; }
            set { NotifyPropertyChanged(ref _wowWindowY, ref value, nameof(WowWindowY)); }
        }

        private WowRegion _region;

        public WowRegion Region
        {
            get { return _region; }
            set { NotifyPropertyChanged(ref _region, ref value, nameof(Region)); }
        }

        public override SettingsBase ShadowCopy()
        {
            return (SettingsBase)MemberwiseClone();
        }

        public override void LoadFromXml(XElement element)
        {
            try
            {
                IsLoaded = false;
                LoginData = GetElementValue<string>(element.Element("LoginData"));
                PasswordData = GetElementValue<string>(element.Element("PasswordData"));
                AcountName = GetElementValue<string>(element.Element("AcountName"));
                CharacterName = GetElementValue<string>(element.Element("CharacterName"));
                ServerName = GetElementValue<string>(element.Element("ServerName"));
                AuthenticatorSerialData = GetElementValue<string>(element.Element("AuthenticatorSerialData"));
                AuthenticatorRestoreCodeData = GetElementValue<string>(element.Element("AuthenticatorRestoreCodeData"));
                Region = GetElementValue<WowSettings.WowRegion>(element.Element("Region"));
                WowPath = GetElementValue<string>(element.Element("WowPath"));
                WowArgs = GetElementValue<string>(element.Element("WowArgs"));
                WowWindowWidth = GetElementValue<int>(element.Element("WowWindowWidth"));
                WowWindowHeight = GetElementValue<int>(element.Element("WowWindowHeight"));
                WowWindowX = GetElementValue<int>(element.Element("WowWindowX"));
                WowWindowY = GetElementValue<int>(element.Element("WowWindowY"));
            }
            finally
            {
                IsLoaded = true;
            }
        }

        public override XElement ConvertToXml()
        {
            var xml = new XElement("WowSettings");
            // Wow Settings 
            xml.Add(new XElement("LoginData", LoginData));
            xml.Add(new XElement("PasswordData", PasswordData));
            xml.Add(new XElement("AcountName", AcountName));
            xml.Add(new XElement("CharacterName", CharacterName));
            xml.Add(new XElement("ServerName", ServerName));
            xml.Add(new XElement("AuthenticatorSerialData", AuthenticatorSerialData));
            xml.Add(new XElement("AuthenticatorRestoreCodeData", AuthenticatorRestoreCodeData));
            xml.Add(new XElement("Region", Region));
            xml.Add(new XElement("WowPath", WowPath));
            xml.Add(new XElement("WowArgs", WowArgs));
            xml.Add(new XElement("WowWindowWidth", WowWindowWidth));
            xml.Add(new XElement("WowWindowHeight", WowWindowHeight));
            xml.Add(new XElement("WowWindowX", WowWindowX));
            xml.Add(new XElement("WowWindowY", WowWindowY));
            return xml;
        }


        #region Embeded type - WowRegion

        public enum WowRegion
        {
            Auto,
            US,
            EU,
            Korea,
            China,
            Taiwan
        }

        #endregion

    }
}
