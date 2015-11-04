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
using System.Windows;

namespace HighVoltz.HBRelog.Settings
{
    public class HonorbuddySettings : INotifyPropertyChanged
    {
        public HonorbuddySettings()
        {
            CustomClass = "Singular";
			HonorbuddyPath = HonorbuddyProfile = HonorbuddyKey = HonorbuddyArgs =  "";
        }

        private string _honorbuddyPath;
        /// <summary>
        /// Path to the Honorbuddy executable
        /// </summary>
        public string HonorbuddyPath
        {
            get { return _honorbuddyPath; }
			set { NotifyPropertyChanged(ref _honorbuddyPath, ref value, nameof(HonorbuddyPath)); }
        }

        string _botBase;
        /// <summary>
        /// Name of the bot to use
        /// </summary>
        public string BotBase
        {
            get { return _botBase; }
			set { NotifyPropertyChanged(ref _botBase, ref value, nameof(BotBase)); }
        }

		private string _honorbuddyKeyData;

	    public string HonorbuddyKeyData
	    {
		    get { return _honorbuddyKeyData; }
		    set { _honorbuddyKeyData = value; }
	    }

	    /// <summary>
	    /// The Honorbuddy Key to use. It can be left empty
	    /// </summary>
	    public string HonorbuddyKey
        {
            get
            {
                try
                {
	                if (string.IsNullOrEmpty(HonorbuddyKeyData))
		                return "";

					return GlobalSettings.Instance.EncryptSettings
						? Utility.DecrptDpapi(HonorbuddyKeyData)
						: HonorbuddyKeyData;
                }
                catch
                {
                    // this error can occur if the Windows password was changed or profile was copied to another computer
                    MessageBox.Show("Error decrypting Honrobuddy key. Try setting Honrobuddy key again.");
                    return "";
                }
            }
            set
            {
				string val = GlobalSettings.Instance.EncryptSettings ? Utility.EncrptDpapi(value) : value;
				NotifyPropertyChanged(ref _honorbuddyKeyData, ref val, nameof(HonorbuddyKey));
            }
        }
        private string _honorbuddyProfile;

        /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public string HonorbuddyProfile
        {
            get { return _honorbuddyProfile; }
			set { NotifyPropertyChanged(ref _honorbuddyProfile, ref value, nameof(HonorbuddyProfile)); }
        }
        private string _customClass;
        /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public string CustomClass
        {
            get { return _customClass; }
			set { NotifyPropertyChanged(ref _customClass, ref value, nameof(CustomClass)); }
        }

		private string _honorbuddyArgs;
		/// <summary>
		/// The Honorbuddy CustomClass to use. It can be left empty
		/// </summary>
		public string HonorbuddyArgs
		{
			get { return _honorbuddyArgs; }
			set { NotifyPropertyChanged(ref _honorbuddyArgs, ref value, nameof(HonorbuddyArgs)); }
		}

        private bool _useHBBeta;

	    /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public bool UseHBBeta
        {
            get { return _useHBBeta; }
			set { NotifyPropertyChanged(ref _useHBBeta, ref value, nameof(UseHBBeta)); }
        }
        public HonorbuddySettings ShadowCopy()
        {
            return (HonorbuddySettings)MemberwiseClone();
        }


        public event PropertyChangedEventHandler PropertyChanged;
		private bool NotifyPropertyChanged<T>(ref T oldValue, ref T newValue, string propertyName)
		{
			if (Equals(oldValue, newValue))
				return false;
			oldValue = newValue;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			if (GlobalSettings.Instance != null)
				GlobalSettings.Instance.QueueSave();
			return true;
		}
	}
}
