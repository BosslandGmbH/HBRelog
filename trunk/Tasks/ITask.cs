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

namespace HighVoltz.HBRelog.Tasks
{
    interface IBMTask : INotifyPropertyChanged
    {
        string Help { get; }
        bool IsDone { get; }
        bool IsRunning { get; }
        string Name { get; }
        string ToolTip { get; }
        CharacterProfile Profile { get; }
        void SetProfile(CharacterProfile profile);
        void Pulse();
        void Start();
        void Stop();
        void Reset();
    }
}
