using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighVoltz.HBRelog.Tasks
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class CustomTaskEditControlAttribute : Attribute
	{
		public CustomTaskEditControlAttribute(Type controlType)
		{
			ControlType = controlType;
		}
		public Type ControlType { get; private set; }
	}
}
