using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using Shared;

namespace WowClient
{
    [Serializable]
    public class WowSettings : INotifyPropertyChanged
    {
        public WowSettings()
        {
            Login = "Email@battle.net";
            Password = Realm = AuthenticatorSerial = AuthenticatorRestoreCode = "";
            AccountName = "WoW1";
            Region = WowRegion.Auto;
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Login)
                && !string.IsNullOrWhiteSpace(Password)
                && !string.IsNullOrWhiteSpace(CharacterName)
                && !string.IsNullOrWhiteSpace(AccountName);
        }

        public string LoginData { get; set; }
        /// <summary>
        /// The Battlenet email address
        /// </summary>
        [XmlIgnore]
        public string Login
        {
            get
            {
                try
                {
                    return Utility.DecrptDpapi(LoginData);
                }
                catch
                {
                    // this error can occur if the Windows password was changed or profile was copied to another computer
                    throw new Exception(string.Format("Error decrypting login for {0}. Try setting login again.",
                        CharacterName));
                }
            }
            set
            {
                LoginData = Utility.EncrptDpapi(value);
                NotifyPropertyChanged("Login");
            }
        }


        public string PasswordData { get; set; }
        
        [XmlIgnore]
        public string Password
        {
            get
            {
                try
                {
                    var pass = Utility.DecrptDpapi(PasswordData);
                    return Utility.DecrptDpapi(PasswordData).Substring(0, Math.Min(16, pass.Length));
                }
                catch
                {
                    MessageBox.Show(string.Format("Error decrypting password for {0}. Try setting password again.", CharacterName));
                    return "";
                }
            }
            set
            {
                PasswordData = Utility.EncrptDpapi(value);
                NotifyPropertyChanged("Password");
            }
        }

        private string _accountName;
        /// <summary>
        /// The name of the wow account. Only used if the battlenet acount has 2+ acounts attached.
        /// </summary>
        public string AccountName
        {
            get { return _accountName; }
            set { _accountName = value; NotifyPropertyChanged("AccountName"); }
        }

		public string AuthenticatorRestoreCodeData { get; set; }

        [XmlIgnore]
        public string AuthenticatorRestoreCode
        {
	        get
	        {
				if (string.IsNullOrEmpty(AuthenticatorRestoreCodeData))
					return "";
				try
				{
                    return Utility.DecrptDpapi(AuthenticatorRestoreCodeData);
				}
				catch
				{
					MessageBox.Show(string.Format("Error decrypting Authenticator Restore code for {0}. Try setting the restore code again.", CharacterName));
					return "";
				}
	        }
	        set
	        {
                AuthenticatorRestoreCodeData = Utility.EncrptDpapi(value);
				NotifyPropertyChanged("AuthenticatorRestoreCode");
	        }
        }

		public string AuthenticatorSerialData { get; set; }

        [XmlIgnore]
		public string AuthenticatorSerial
		{
			get
			{
				if (string.IsNullOrEmpty(AuthenticatorSerialData))
					return "";
				try
				{
                    return Utility.DecrptDpapi(AuthenticatorSerialData);
				}
				catch
				{
					MessageBox.Show(string.Format("Error decrypting Authenticator Serial for {0}. Try setting the serial again.", CharacterName));
					return "";
				}
			}
			set
			{
                AuthenticatorSerialData = Utility.EncrptDpapi(value);
				NotifyPropertyChanged("AuthenticatorSerial");
			}
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

        private List<string> _accountCharacterNames;
        /// <summary>
        /// Contains a character name list seen on the account.
        /// </summary>
        public List<string> AccountCharacterNames
        {
            get { return _accountCharacterNames; }
            set { _accountCharacterNames = value; NotifyPropertyChanged("AccountCharacterNames"); }
        }

        private string _realm;
        /// <summary>
        /// Name of the WoW server
        /// </summary>
        public string Realm
        {
            get { return _realm; }
            set { _realm = value; NotifyPropertyChanged("Realm"); }
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

        private int _wowWindowLeft;
        public int WowWindowLeft
        {
            get { return _wowWindowLeft; }
            set { _wowWindowLeft = value; NotifyPropertyChanged("WowWindowLeft"); }
        }

        private int _wowWindowTop;
        public int WowWindowTop
        {
            get { return _wowWindowTop; }
            set { _wowWindowTop = value; NotifyPropertyChanged("WowWindowTop"); }
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
        }

        public enum WowRegion
        {
            Auto,
            US,
            EU,
            Korea,
            China,
            Taiwan
        }
    }
}
