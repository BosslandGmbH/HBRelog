using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.Remoting
{
    interface IRemotingApi
    {
        bool Init(int hbProcID);
        void RestartHB(int hbProcID);
        void RestartWow(int hbProcID);
        string[] GetProfileNames();
        string GetCurrentProfileName(int hbProcID);
        void StartProfile(string profileName);
        void StopProfile(string profileName);
        void PauseProfile(string profileName);
        void IdleProfile(string profileName, TimeSpan time);
        void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath);

        int GetProfileStatus(string profileName);
        void SetProfileStatusText(int hbProcID, string status);
    }
}
