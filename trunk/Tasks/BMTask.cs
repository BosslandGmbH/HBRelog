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
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace HighVoltz.HBRelog.Tasks
{
    [DataContract]
    abstract public class BMTask : IBMTask
    {
        bool _isDone;
        [XmlIgnore]
        public bool IsDone
        {
            get { return _isDone; }
            protected set
            {
                _isDone = value;
                if (_isDone && _isRunning)
                    Stop();
                OnPropertyChanged("IsDone");
            }
        }

        bool _isRunning;
        [XmlIgnore]
        public bool IsRunning
        {
            get { return _isRunning; }
            protected set
            {
                _isRunning = value;
                OnPropertyChanged("IsRunning");
            }
        }
        [XmlIgnore]
        public CharacterProfile Profile { get; private set; }

        [XmlIgnore]
        abstract public string Name { get; }

        protected BMTask NextTask
        {
            get
            {
                int index = Profile.Tasks.IndexOf(this);
                if (index >= 0)
                {
                    return index + 1 >= Profile.Tasks.Count ? Profile.Tasks[0] : Profile.Tasks[index + 1];
                }
                return null;
            }
        }

        protected BMTask PrevTask
        {
            get
            {
                int index = Profile.Tasks.IndexOf(this);
                if (index >= 0)
                {
                    return index == 0 ? Profile.Tasks[Profile.Tasks.Count - 1] : Profile.Tasks[index - 1];
                }
                return null;
            }
        }

        protected Process BotProcess { get { return Profile.TaskManager.HonorbuddyManager.BotProcess; } }

        protected Process GameProcess { get { return Profile.TaskManager.WowManager.GameProcess; } }

        public void SetProfile(CharacterProfile profile)
        {
            Profile = profile;
        }
        abstract public void Pulse();
        /// <summary>
        /// resets any variables to their default values. Any overrides must call base
        /// </summary>
        public virtual void Reset()
        {
            IsDone = false;
        }
        /// <summary>
        /// Called by the TaskManager when Task is started. Any overrides of this method needs to call base
        /// </summary>
        public virtual void Start()
        {
            IsRunning = true;
        }
        /// <summary>
        /// sets IsDone to true and IsRunning to false. Any overrides need to call base.
        /// </summary>
        public virtual void Stop()
        {
            IsRunning = false;
            IsDone = true;
        }

        public override string ToString()
        {
            return Name;
        }

        abstract public string Help { get; }

        abstract public string ToolTip { get; set; }

        public BMTask ShadowCopy()
        {
            return (BMTask)MemberwiseClone();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
