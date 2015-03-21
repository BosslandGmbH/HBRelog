using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighVoltz.HBRelog.Tasks
{
	public interface ICustomTaskEditControlDataBound
	{
		void SetBinding(BMTask source, string path);
		void SetValue(object value);
	}
}
