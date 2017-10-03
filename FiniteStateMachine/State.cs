using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.FiniteStateMachine
{
    public abstract class State
    {
        public abstract int Priority { get; }

        public abstract bool NeedToRun { get; }

        public abstract void Run();
    }
}
