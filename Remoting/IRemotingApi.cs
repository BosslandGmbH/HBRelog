using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace HighVoltz.HBRelog.Remoting
{
    [ServiceContract]
    internal interface IRemotingApi
    {
        [OperationContract]
        bool Init(int hbProcId);

        [OperationContract(IsOneWay = true)]
        void Heartbeat(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void RestartHB(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void RestartWow(int hbProcID);

        [OperationContract]
        string[] GetProfileNames();

        [OperationContract]
        string GetCurrentProfileName(int hbProcID);

        [OperationContract(IsOneWay = true)]
        void StartProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void StopProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void PauseProfile(string profileName);

        [OperationContract(IsOneWay = true)]
        void IdleProfile(string profileName, TimeSpan time);

        [OperationContract(IsOneWay = true)]
        void Logon(int hbProcID, string character, string server, string customClass, string botBase, string profilePath);

        [OperationContract]
        int GetProfileStatus(string profileName);

        [OperationContract(IsOneWay = true)]
        void SetProfileStatusText(int hbProcID, string status);

        [OperationContract(IsOneWay = true)]
        void ProfileLog(int hbProcID, string msg);


        [OperationContract(IsOneWay = true)]
        void SetBotInfoToolTip(int hbProcID, string tooltip);

        [OperationContract(IsOneWay = true)]
        void SkipCurrentTask(string profileName);
    }
}
