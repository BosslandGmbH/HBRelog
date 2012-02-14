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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace HighVoltz.Tasks
{
    public class IdleTask : BMTask
    {
        public int Minutes { get; set; }
        public int RandomMinutes { get; set; }
        [XmlIgnore]
        public override string Name
        {
            get { return "Idle"; }
        }

        TimeSpan _waitTime = new TimeSpan(0);
        DateTime _timeStamp;
        public override void Pulse()
        {
            if (_waitTime == TimeSpan.FromTicks(0))
            {
                _waitTime = TimeSpan.FromMinutes(Minutes + Utility.Rand.Next(-RandomMinutes, RandomMinutes));
                Profile.Log("Idling for {0} minutes before executing next task", _waitTime.TotalMinutes);
                _timeStamp = DateTime.Now;
                if (Profile.TaskManager.WowManager.IsRunning)
                    Profile.TaskManager.WowManager.Stop();
                if (Profile.TaskManager.HonorbuddyManager.IsRunning)
                    Profile.TaskManager.HonorbuddyManager.Stop();
            }
            Profile.Status = string.Format("Idling for {0} minutes", (int)((_waitTime - (DateTime.Now - _timeStamp)).TotalMinutes));
            if (DateTime.Now - _timeStamp >= _waitTime)
            {
                IsDone = true;
                Profile.Start();
                Profile.Log("Idle complete");
            }
        }

        public override void Reset()
        {
            base.Reset();
            _waitTime = new TimeSpan(0);
        }
    }
}
