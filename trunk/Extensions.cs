using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighVoltz.HBRelog
{
    internal static class Extensions
    {
        public static bool HasExitedSafe(this Process process)
        {
            if (process == null)
                return true;

            bool hasExited;
            try
            {
                hasExited = process.HasExited;
            }
            catch
            {
                hasExited = true;
            }
            return hasExited;
        }
    }
}
