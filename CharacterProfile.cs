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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using HighVoltz.HBRelog.Tasks;
using HighVoltz.HBRelog.Settings;
using System.Xml.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Xml.Serialization;
using System.Globalization;
using System.Diagnostics;

namespace HighVoltz.HBRelog
{
    sealed public class CharacterProfile : INotifyPropertyChanged
    {
        public ProfileSettings Settings { get; set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public readonly TaskManager TaskManager;
        public ObservableCollection<BMTask> Tasks { get; private set; }

        public CharacterProfile()
        {
            Settings = new ProfileSettings();
            Tasks = new ObservableCollection<BMTask>();
            TaskManager = new TaskManager(this);
        }
        private string _status;
        /// <summary>
        /// Status message
        /// </summary>
        public string Status
        {
            get { return _status; }
            set { _status = value; NotifyPropertyChanged("Status"); }
        }
        private string _tooltip;
        /// <summary>
        /// Tooltip message
        /// </summary>
        public string Tooltip
        {
            get { return _tooltip; }
            set
            {
                if (value != _tooltip)
                {
                    _tooltip = value;
                    NotifyPropertyChanged("Tooltip");
                }
            }
        }

        private string _taskTooltip;
        /// <summary>
        /// Current Task Tooltip message. Displayed in main ToolTip
        /// </summary>
        public string TaskTooltip
        {
            get { return _taskTooltip; }
            set
            {
                if (value != _taskTooltip)
                {
                    _taskTooltip = value;
                    UpdateTooltip();
                }
            }
        }

        private string _botInfoTooltip;
        /// <summary>
        /// Bot info Tooltip message. Displayed in main ToolTip
        /// </summary>
        public string BotInfoTooltip
        {
            get { return _botInfoTooltip; }
            set
            {
                if (value != _botInfoTooltip)
                {
                    _botInfoTooltip = value;
                    UpdateTooltip();
                }
            }
        }

        void UpdateTooltip()
        {
            Tooltip = string.Format("{0}{1}",
                        !string.IsNullOrEmpty(TaskTooltip) ? TaskTooltip + "\n" : null,
                        BotInfoTooltip);
        }

        public void Pulse()
        {
            if (IsRunning && !IsPaused)
            {
                TaskManager.Pulse();
            }
        }

        public void Pause()
        {
            Status = "Paused";
            if (TaskManager.WowManager.LockToken != null && TaskManager.WowManager.LockToken.IsValid)
            {
                TaskManager.WowManager.LockToken.ReleaseLock();
            }

            IsPaused = true;
        }

        public void Start()
        {
            Status = "Running";
            if (!IsPaused)
                TaskManager.Start();
            IsRunning = true;
            IsPaused = false;
        }

        public void Stop()
        {
            Status = "Stopped";
            TaskManager.Stop();
            IsRunning = false;
            IsPaused = false;
        }

        private string _lastLog;
        public void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }

        public void Log(string msg)
        {
            if (msg == _lastLog)
                return;
            _lastLog = msg;

            if (HbRelogManager.Settings.UseDarkStyle)
                HBRelog.Log.Write(Colors.LightBlue, Settings.ProfileName + ": ", Colors.LightGreen, "{0}", msg);
            else
                HBRelog.Log.Write(Colors.DarkSlateBlue, Settings.ProfileName + ": ", Colors.DarkGreen, "{0}", msg);
        }

        public void Err(string format, params object[] args)
        {
            Err(string.Format(format, args));
        }

        public void Err(string msg)
        {
            HBRelog.Log.Write(HbRelogManager.Settings.UseDarkStyle ? Colors.LightBlue : Colors.DarkSlateBlue,
                                        Settings.ProfileName + ": ", Colors.Red, msg);
        }

        public CharacterProfile ShadowCopy()
        {
            var cp = (CharacterProfile)MemberwiseClone();
            cp.Tasks = new ObservableCollection<BMTask>();
            foreach (var bmTask in Tasks)
            {
                cp.Tasks.Add(bmTask.ShadowCopy());
            }
            cp.Settings = (ProfileSettings)Settings.ShadowCopy();
            return cp;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void LoadFromXml(XElement element)
        {
            Settings.LoadFromXml(element.Element("Settings"));
            
            // Tasks
            XElement tasksElement = element.Element("Tasks");
            foreach (XElement taskElement in tasksElement.Elements())
            {
                Type taskType = Type.GetType("HighVoltz.HBRelog.Tasks." + taskElement.Name);
                if (taskType != null)
                {
                    var task = (BMTask)Activator.CreateInstance(taskType);
                    task.SetProfile(this);
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
                                             : Convert.ChangeType(attr.Value, propertyDict[propKey].PropertyType, CultureInfo.InvariantCulture);
                            propertyDict[propKey].SetValue(task, val, null);
                        }
                        else
                        {
                            Err("{0} does not have a property called {1}", taskElement.Name, attr.Name);
                        }
                    }
                    Tasks.Add(task);
                }
                else
                {
                    Err("{0} is not a known task type", taskElement.Name);
                }
            }
        }

        public XElement ConvertToXml()
        {
            var xml = new XElement("CharacterProfile");
            var settingsElement = Settings.ConvertToXml();
            xml.Add(settingsElement);

            // Tasks
            var tasksElement = new XElement("Tasks");
            foreach (BMTask task in Tasks)
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
            xml.Add(tasksElement);
            return xml;
        }
    }
}
