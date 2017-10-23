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
    public abstract class SettingsBase : INotifyPropertyChanged
    {
        /// <summary>Must be set in the Load overide. It's used for disabling change notification while loading </summary>
        protected bool IsLoaded { get; set; }

        public abstract void LoadFromXml(XElement element);
        public abstract XElement ConvertToXml();

        public abstract SettingsBase ShadowCopy();

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected bool NotifyPropertyChanged<T>(ref T oldValue, ref T newValue, string propertyName)
        {
            if (Equals(oldValue, newValue))
                return false;
            oldValue = newValue;
            if (IsLoaded)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                if (HbRelogManager.Settings != null)
                    HbRelogManager.Settings.QueueSave();
            }
            return true;
        } 

        #endregion

        protected T GetElementValue<T>(XElement element, T defaultValue = default(T))
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

    }
}