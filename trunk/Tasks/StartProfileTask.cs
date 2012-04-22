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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace HighVoltz.HBRelog.Tasks
{
    class StartProfileTask : BMTask
    {
        [XmlIgnore]
        public override string Name { get { return "StartProfile"; } }

        [XmlIgnore]
        override public string Help { get { return "Starts a profle"; } }

        string _toolTip;
        [XmlIgnore]
        public override string ToolTip
        {
            get
            {
                return _toolTip ?? (ToolTip = string.Format("Start profile: {0}", ProfileName));
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

        public string ProfileName { get; set; }

        public override void Pulse()
        {
            var profile = HbRelogManager.Settings.CharacterProfiles
                .FirstOrDefault(p => p.Settings.ProfileName.Equals(ProfileName, StringComparison.InvariantCultureIgnoreCase));
            if (profile != null)
            {
                Profile.Log("Starting profile: {0}", ProfileName);
                profile.Start();
            }
            else
            {
                Profile.Err("Could not find a profile named {0}", ProfileName);
            }
            IsDone = true;
        }
    }
}
