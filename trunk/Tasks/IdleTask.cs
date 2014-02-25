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

namespace HighVoltz.HBRelog.Tasks
{
    public class IdleTask : BMTask
    {
	    #region Overrides

	    [XmlIgnore]
	    public override string Name
	    {
		    get { return "Idle"; }
	    }

	    [XmlIgnore]
	    public override string Help
	    {
		    get { return "Logs out of Wow for a duration then logs back in"; }
	    }

	    private string _toolTip;

	    [XmlIgnore]
	    public override string ToolTip
	    {
		    get
		    {
			    return _toolTip ?? (ToolTip = string.Format("Idle: {0} minutes", Minutes));
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

	    private TimeSpan _waitTime = new TimeSpan(0);
	    private DateTime _timeStamp;

	    public override void Pulse()
	    {
		    if (_waitTime == TimeSpan.FromTicks(0))
		    {
			    _waitTime = TimeSpan.FromMinutes(Minutes + Utility.Rand.Next(-RandomMinutes, RandomMinutes));
			    Profile.Log("Idling for {0} minutes before executing next task", _waitTime.TotalMinutes);
			    _timeStamp = DateTime.Now;
			    Profile.TaskManager.WowManager.Stop();
			    Profile.TaskManager.HonorbuddyManager.Stop();
		    }
		    Profile.Status = string.Format(
			    "Idling for {0} minutes",
			    (int) ((_waitTime - (DateTime.Now - _timeStamp)).TotalMinutes));
		    ToolTip = Profile.Status;
		    if (DateTime.Now - _timeStamp >= _waitTime)
		    {
			    IsDone = true;
			    // if next task is not a LogonTask then we log back in game on same character.
			    if (!(NextTask is LogonTask))
				    Profile.Start();
			    Profile.Log("Idle complete");
		    }
	    }

	    public override void Reset()
	    {
		    base.Reset();
		    _waitTime = new TimeSpan(0);
		    ToolTip = string.Format("Idle: {0} minutes", Minutes);
	    }

	    #endregion

		private int _minutes;
		public int Minutes
		{
			get { return _minutes; }
			set
			{
				if (value == _minutes) return;
				_minutes = value;
				// invalidate the tooltip.
				_toolTip = null;
			}
		}

		private int _randomMinutes;
		public int RandomMinutes
		{
			get { return _randomMinutes; }
			set
			{
				if (value == _randomMinutes) return;
				_randomMinutes = value;
				// invalidate the tooltip.
				_toolTip = null;
			}
		}

    }
}
