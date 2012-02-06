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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace HighVoltz.Tasks
{
    [DataContract]
    abstract public class BMTask : IBMTask
    {
        [XmlIgnore]
        virtual public bool IsDone { get; protected set; }
        [XmlIgnore]
        public CharacterProfile Profile { get; private set; }
        [XmlIgnore]
        abstract public string Name { get; }
        public void SetProfile(CharacterProfile profile)
        {
            Profile = profile;
        }
        abstract public void Pulse();
        public virtual void Reset()
        {
            IsDone = false;
        }
        public override string ToString()
        {
            return Name;
        }
    }
}
