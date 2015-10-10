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
using System.Xml.Serialization;
using HighVoltz.HBRelog.Remoting;

namespace HighVoltz.HBRelog.Tasks
{
    public class WaitBotStopTask : BMTask
    {
        #region Overrides

        [XmlIgnore]
        public override string Name
        {
            get { return "WaitBotStop"; }
        }

        [XmlIgnore]
        public override string Help
        {
            get { return "Waits for bot to stop by any means (profile ends, pushed stop button)"; }
        }

        private string _toolTip;
        private bool _isNotInitialized = true;

        [XmlIgnore]
        public override string ToolTip
        {
            get
            {
                return _toolTip ?? (_toolTip = string.Format("Wait till bot stops"));
            }
            set
            {
                if (value != _toolTip)
                {
                    _toolTip = value;
                    OnPropertyChanged("ToolTip");
                }
            }
        }

        public WaitBotStopTask() : base()
        {
        }

        public override void Pulse()
        {
            if (_isNotInitialized)
            {
                HbRelogManager.remoting.OnBotStoppedEvent += remoting_OnBotStoppedEvent;
                _isNotInitialized = false;
            }
        }

        void remoting_OnBotStoppedEvent(object sender, EventArgs e)
        {
            var args = e as BotStoppedEventArgs;
            if (args != null
                && args.HbProcessId == Profile.TaskManager.HonorbuddyManager.BotProcess.Id)
            {
                IsDone = true;
                Profile.Log("WaitBotStop complete");
            }
        }

        public override void Reset()
        {
            IsDone = false;
        }

        #endregion

    }
}
