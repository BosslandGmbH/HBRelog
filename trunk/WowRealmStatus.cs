using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog
{
    [DataContract]
    public class WowRealmStatus
    {
        object _lockObject = new object();
        public WowRealmStatus()
        {
            Realms = new Dictionary<string, WowReamStatusEntry>();
        }

        public WowReamStatusEntry this[string realm]
        {
            get
            {
                lock (_lockObject)
                {
                    string key = realm.ToUpper();
                    return Realms.ContainsKey(key) ? Realms[key] : null;
                }
            }
        }

        public Dictionary<string, WowReamStatusEntry> Realms { get; private set; }

        [DataMember(Name = "realms")]
        List<WowReamStatusEntry> realmStatusList { get; set; }

        Task _updateTask;
        const string WowStatusApiBaseUrl = "http://www.battle.net/api/wow/realm/status?realms=";
        string _wowStatusApiUrl;
        public void Update()
        {
            if (_updateTask == null || _updateTask.Status == TaskStatus.RanToCompletion)
            {  // update url in main thread to prevent concurrency issues.
                _wowStatusApiUrl = GetWowRealmStatusApiUrl();
                _updateTask = new Task(UpdateWowRealmStatus);
                _updateTask.Start();
            }
        }

        public bool RealmIsOnline(string realm)
        {
            var status = this[realm];
            return status != null && status.IsOnline;
        }

        void UpdateWowRealmStatus()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_wowStatusApiUrl);
                request.GetResponse();
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        var serialiser = new DataContractJsonSerializer(typeof(WowRealmStatus));
                        WowRealmStatus result = (WowRealmStatus)serialiser.ReadObject(stream);
                        lock (_lockObject)
                        {
                            Realms = result.realmStatusList.ToDictionary(s => s.Name.ToUpper());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Err(ex.ToString());
            }
        }

        string GetWowRealmStatusApiUrl()
        {
            List<string> serverList = new List<string>();
            foreach (var profile in HBRelogManager.Settings.CharacterProfiles)
            {
                string server = profile.Settings.WowSettings.ServerName;
                if (!serverList.Contains(server))
                    serverList.Add(server);
                List<LogonTask> logonList = profile.Tasks.Where(t => t is LogonTask).
                    Cast<LogonTask>().ToList();
                foreach (LogonTask logon in logonList)
                {
                    if (!string.IsNullOrEmpty(logon.Server) && !serverList.Contains(logon.Server))
                        serverList.Add(server);
                }
            }
            string ret = "";
            for (int i = 0; i < serverList.Count; i++)
            {// don't append a comma (,) if at the end of the list.
                ret += i != serverList.Count - 1 ? serverList[i] + "," : serverList[i];
            }
            return WowStatusApiBaseUrl + ret;
        }

        [DataContract]
        public class WowReamStatusEntry
        {
            [DataMember(Name = "type")]
            public string Type { get; private set; }
            [DataMember(Name = "population")]
            public string Population { get; private set; }
            [DataMember(Name = "queue")]
            public bool InQueue { get; private set; }
            [DataMember(Name = "status")]
            public bool IsOnline { get; private set; }
            [DataMember(Name = "name")]
            public string Name { get; private set; }
            [DataMember(Name = "slug")]
            public string Slug { get; private set; }
            [DataMember(Name = "battlegroup")]
            public string Battlegroup { get; private set; }
        }
    }
}
