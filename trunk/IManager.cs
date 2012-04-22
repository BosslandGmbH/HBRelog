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

namespace HighVoltz.HBRelog
{
    interface IManager
    {
        /// <summary>
        /// The Character profile that belongs to this manager
        /// </summary>
        [XmlIgnore]
        CharacterProfile Profile { get;}
        /// <summary>
        /// Manager is running.
        /// </summary>
        [XmlIgnore]
        bool IsRunning { get; }
        /// <summary>
        /// Set to true once the startup sequence is complete
        /// </summary>
        [XmlIgnore]
        bool StartupSequenceIsComplete { get; }
        event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;

        /// <summary>
        /// Starts the Manager
        /// </summary>
        void Start();
        /// <summary>
        /// Pulses the Manager
        /// </summary>
        void Pulse();

        /// <summary>
        /// Stops the Manager
        /// </summary>
        void Stop();
    }
}
