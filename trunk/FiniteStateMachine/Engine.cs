using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.FiniteStateMachine
{
    namespace FiniteStateMachine
    {
        public class Engine
        {
            protected Engine(List<State> states = null)
            {
                States = states ?? new List<State>();
            }

            private List<State> _states;
            public List<State> States
            {
                get { return _states; }
                protected set
                {
                    _states = value;
                    // Remember: We implemented the IComparer, and IComparable
                    // interfaces on the State class!
                    if (_states != null)
                        _states.Sort();
                }
            }

            public virtual bool IsRunning { get; protected set; }

            public virtual void Pulse()
            {
                // This starts at the highest priority state,
                // and iterates its way to the lowest priority.
                foreach (State state in States)
                {
                    if (state.NeedToRun)
                    {
                        state.Run();
                        // Break out of the iteration,
                        // as we found a state that has run.
                        // We don't want to run any more states
                        // this time around.
                        break;
                    }
                }
            }
        }
    }
}