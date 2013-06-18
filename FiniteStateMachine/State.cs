using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.FiniteStateMachine
{
    public abstract class State : IComparable<State>, IComparer<State>
    {
        public abstract int Priority { get; }

        public abstract bool NeedToRun { get; }

        public abstract void Run();

        public int CompareTo(State other)
        {
            // We want the highest first.
            // int, by default, chooses the lowest to be sorted
            // at the bottom of the list. We want the opposite.
            return -Priority.CompareTo(other.Priority);
        }

        public int Compare(State x, State y)
        {
            return -x.Priority.CompareTo(y.Priority);
        }
    }
}
