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

using System.Diagnostics;
using System.Xml.Serialization;
using HighVoltz.HBRelog.Settings;

namespace HighVoltz.HBRelog
{
    interface IBotManager : IManager
    {
        /// <summary>
        /// Process that the AppManager is controling
        /// </summary>
        [XmlIgnore]
        Process BotProcess { get; }
        void SetSettings(HonorbuddySettings settings);
        void SetStartupSequenceToComplete();
    }
}
