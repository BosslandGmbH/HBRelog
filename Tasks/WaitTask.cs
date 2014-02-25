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
    public class WaitTask : BMTask
    {
	    #region Overrides

	    [XmlIgnore]
	    public override string Name
	    {
		    get { return "Wait"; }
	    }

	    [XmlIgnore]
	    public override string Help
	    {
		    get { return "Waits for duration before next task is executed"; }
	    }

	    private string _toolTip;

	    [XmlIgnore]
	    public override string ToolTip
	    {
		    get
		    {
			    return _toolTip ?? (_toolTip = string.Format("Wait: {0} minutes", Minutes));
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
			    Profile.Log("Waiting for {0} minutes before executing next task", _waitTime.TotalMinutes);
			    _timeStamp = DateTime.Now;
		    }

		    BMTask nextTask = NextTask;
		    if (nextTask != null)
			    ToolTip = string.Format(
				    "Running {0} task in {1} minutes",
				    nextTask,
				    (int) ((_waitTime - (DateTime.Now - _timeStamp)).TotalMinutes));


		    if (DateTime.Now - _timeStamp >= _waitTime)
		    {
			    IsDone = true;
			    Profile.Log("Wait complete");
			    ToolTip = string.Format("Wait: {0} minutes", Minutes);
		    }
	    }

	    public override void Reset()
	    {
		    base.Reset();
		    _waitTime = new TimeSpan(0);
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
