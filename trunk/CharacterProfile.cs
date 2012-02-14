﻿/*
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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
using HighVoltz.Settings;
using HighVoltz.Tasks;
using HighVoltz.WoW;

namespace HighVoltz
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

        public void Log(string format, params object[] args)
        {
            HighVoltz.Log.Write(Colors.DarkSlateBlue, Settings.ProfileName + ": ", Colors.DarkGreen, format, args);
        }

        public void Err(string format, params object[] args)
        {
            HighVoltz.Log.Write(Colors.DarkSlateBlue, Settings.ProfileName + ": ", Colors.Red, format, args);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
