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
    public class HonorbuddySettings : INotifyPropertyChanged
    {
        public HonorbuddySettings()
        {
            HonorbuddyPath = string.Empty;
            CustomClass = "Singular";
            BotBase = string.Empty;
            HonorbuddyProfile = string.Empty;
	        HonorbuddyArgs = string.Empty;
        }

        private string _honorbuddyPath;
        /// <summary>
        /// Path to the Honorbuddy executable
        /// </summary>
        public string HonorbuddyPath
        {
            get { return _honorbuddyPath; }
            set { _honorbuddyPath = value; NotifyPropertyChanged("HonorbuddyPath"); }
        }
        string _botBase;
        /// <summary>
        /// Name of the bot to use
        /// </summary>
        public string BotBase
        {
            get { return _botBase; }
            set { _botBase = value; NotifyPropertyChanged("BotBase"); }
        }

        public string HonorbuddyKeyData { get; set; }
        /// <summary>
        /// The Honorbuddy Key to use. It can be left empty
        /// </summary>
        public string HonorbuddyKey
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(HonorbuddyKeyData) ? Utility.DecrptDpapi(HonorbuddyKeyData) : "";
                }
                catch
                {
                    // this error can occur if the Windows password was changed or profile was copied to another computer
                    MessageBox.Show(string.Format("Error decrypting Honrobuddy key. Try setting Honrobuddy key again."));
                    return "";
                }
            }
            set
            {
                HonorbuddyKeyData = Utility.EncrptDpapi(value);
                NotifyPropertyChanged("HonorbuddyKey");
            }
        }
        private string _honorbuddyProfile;

        /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public string HonorbuddyProfile
        {
            get { return _honorbuddyProfile; }
            set { _honorbuddyProfile = value; NotifyPropertyChanged("HonorbuddyProfile"); }
        }
        private string _customClass;
        /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public string CustomClass
        {
            get { return _customClass; }
            set { _customClass = value; NotifyPropertyChanged("CustomClass"); }
        }

		private string _honorbuddyArgs;
		/// <summary>
		/// The Honorbuddy CustomClass to use. It can be left empty
		/// </summary>
		public string HonorbuddyArgs
		{
			get { return _honorbuddyArgs; }
			set { _honorbuddyArgs = value; NotifyPropertyChanged("HonorbuddyArgs"); }
		}

        private bool _useHBBeta;
        /// <summary>
        /// The Honorbuddy CustomClass to use. It can be left empty
        /// </summary>
        public bool UseHBBeta
        {
            get { return _useHBBeta; }
            set { _useHBBeta = value; NotifyPropertyChanged("UseHBBeta"); }
        }
        public HonorbuddySettings ShadowCopy()
        {
            return (HonorbuddySettings)MemberwiseClone();
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
