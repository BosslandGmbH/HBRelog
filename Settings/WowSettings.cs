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
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace HighVoltz.HBRelog.Settings
{
    public class WowSettings : INotifyPropertyChanged
    {
        public WowSettings()
        {
            Login = "Email@battle.net";
            Password = string.Empty;
            ServerName = string.Empty;
            WowPath = string.Empty;
            AcountName = "WoW1";
            Region = WowRegion.Auto;
        }
        public string LoginData { get; set; }
        /// <summary>
        /// The Battlenet email address
        /// </summary>
        public string Login
        {
            get
            {
                try
                {
                    byte[] data = Convert.FromBase64String(LoginData);
                    data = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                    return Encoding.Unicode.GetString(data);
                }
                catch
                {
                    // this error can occur if the Windows password was changed or profile was copied to another computer
                    MessageBox.Show(string.Format("Error decrypting login for {0}. Try setting login again.",
                        CharacterName));
                    return "";
                }
            }
            set
            {
                byte[] data = Encoding.Unicode.GetBytes(value);
                data = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                LoginData = Convert.ToBase64String(data);
                NotifyPropertyChanged("Login");
            }
        }

        public string PasswordData { get; set; }
        public string Password
        {
            get
            {
                try
                {
                    byte[] data = Convert.FromBase64String(PasswordData);
                    data = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                    return Encoding.Unicode.GetString(data);
                }
                catch
                {
                    MessageBox.Show(string.Format("Error decrypting password for {0}. Try setting password again.", CharacterName));
                    return "";
                }
            }
            set
            {
                byte[] data = Encoding.Unicode.GetBytes(value);
                data = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                PasswordData = Convert.ToBase64String(data);
            }
        }

        private string _acountName;
        /// <summary>
        /// The name of the wow account. Only used if the battlenet acount has 2+ acounts attached.
        /// </summary>
        public string AcountName
        {
            get { return _acountName; }
            set { _acountName = value; NotifyPropertyChanged("AcountName"); }
        }

        private string _characterName;
        /// <summary>
        /// The in-game character name
        /// </summary>
        public string CharacterName
        {
            get { return _characterName; }
            set { _characterName = value; NotifyPropertyChanged("CharacterName"); }
        }

        private string _serverName;
        /// <summary>
        /// Name of the WoW server
        /// </summary>
        public string ServerName
        {
            get { return _serverName; }
            set { _serverName = value; NotifyPropertyChanged("ServerName"); }
        }
        private string _wowPath;
        /// <summary>
        /// Path to your WoW.Exe
        /// </summary>
        public string WowPath
        {
            get { return _wowPath; }
            set { _wowPath = value; NotifyPropertyChanged("WowPath"); }
        }
        private int _wowWindowWidth;
        public int WowWindowWidth
        {
            get { return _wowWindowWidth; }
            set { _wowWindowWidth = value; NotifyPropertyChanged("WowWindowWidth"); }
        }

        private int _wowWindowHeight;
        public int WowWindowHeight
        {
            get { return _wowWindowHeight; }
            set { _wowWindowHeight = value; NotifyPropertyChanged("WowWindowHeight"); }
        }

        private int _wowWindowX;
        public int WowWindowX
        {
            get { return _wowWindowX; }
            set { _wowWindowX = value; NotifyPropertyChanged("WowWindowX"); }
        }

        private int _wowWindowY;
        public int WowWindowY
        {
            get { return _wowWindowY; }
            set { _wowWindowY = value; NotifyPropertyChanged("WowWindowY"); }
        }

        private WowRegion _region;
        public WowRegion Region
        {
            get { return _region; }
            set { _region = value; NotifyPropertyChanged("Region"); }
        }

        public WowSettings ShadowCopy()
        {
            return (WowSettings)MemberwiseClone();
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
