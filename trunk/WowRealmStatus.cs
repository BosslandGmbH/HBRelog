using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using HighVoltz.HBRelog.Settings;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog
{
    [DataContract]
    public class WowRealmStatus
    {
        readonly object _lockObject = new object();
        public WowRealmStatus()
        {
            Realms = new List<WowRealmStatusEntry>();
        }

        public WowRealmStatusEntry this[string realm, WowSettings.WowRegion region]
        {
            get
            {
                lock (_lockObject)
                {
                    return Realms.FirstOrDefault(r =>
                        r.Name.Equals(realm, StringComparison.InvariantCultureIgnoreCase) &&
                        r.Region == region);
                }
            }
        }

        string GetKey(string realm, WowSettings.WowRegion region)
        {
            return string.Format("{0}-{1}", realm.ToUpper(), region);
        }

        [DataMember(Name = "realms")]
        public List<WowRealmStatusEntry> Realms { get; private set; }

        Task _updateTask;
        const string WowStatusApiBaseUrl = "http://www.battle.net/api/wow/realm/status?realms=";
        const string USWowStatusApiBaseUrl = "http://us.battle.net/api/wow/realm/status?realms=";
        const string EuWowStatusApiBaseUrl = "http://eu.battle.net/api/wow/realm/status?realms=";
        const string KoreaWowStatusApiBaseUrl = "http://kr.battle.net/api/wow/realm/status?realms=";
        const string TaiwanWowStatusApiBaseUrl = "http://tw.battle.net/api/wow/realm/status?realms=";
        const string ChinaWowStatusApiBaseUrl = "http://www.battlenet.com.cn/api/wow/realm/status?realms=";

        private string GetUrlForRegion(WowSettings.WowRegion region)
        {
            switch (region)
            {
                case WowSettings.WowRegion.China:
                    return ChinaWowStatusApiBaseUrl;
                case WowSettings.WowRegion.EU:
                    return EuWowStatusApiBaseUrl;
                case WowSettings.WowRegion.Korea:
                    return KoreaWowStatusApiBaseUrl;
                case WowSettings.WowRegion.Taiwan:
                    return TaiwanWowStatusApiBaseUrl;
                case WowSettings.WowRegion.US:
                    return USWowStatusApiBaseUrl;
                default:
                    return WowStatusApiBaseUrl;
            }
        }

        public void Update()
        {
            if (_updateTask == null || _updateTask.Status == TaskStatus.RanToCompletion)
            {
                _updateTask = new Task(UpdateWowRealmStatus);
                _updateTask.Start();
            }
        }

        public bool RealmIsOnline(string realm, WowSettings.WowRegion region)
        {
            var status = this[realm, region];
            return status != null && status.IsOnline;
        }

        void UpdateWowRealmStatus()
        {
            IEnumerable<IGrouping<WowSettings.WowRegion, CharacterProfile>> regionGroups = HbRelogManager.Settings.CharacterProfiles.GroupBy(k => k.Settings.WowSettings.Region);
            var taskList = (from @group in regionGroups select CreateRealmUpdateTask(@group.Key, @group)).ToList();
            // wait for the tasks to complete
            Task.WaitAll(taskList.ToArray());
            lock (_lockObject)
            {
                Realms.Clear();
                foreach (var task in taskList)
                {
                    if (task.Result != null)
                        Realms.AddRange(task.Result);
                }
            }
        }

        Task<List<WowRealmStatusEntry>> CreateRealmUpdateTask(WowSettings.WowRegion region, IEnumerable<CharacterProfile> profiles)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    string url = BuildWowRealmStatusApiUrl(GetUrlForRegion(region), profiles);
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.GetResponse();
                    using (WebResponse response = request.GetResponse())
                    {
                        using (Stream stream = response.GetResponseStream())
                        {
                            var serialiser = new DataContractJsonSerializer(typeof(WowRealmStatus));
                            var result = (WowRealmStatus)serialiser.ReadObject(stream);

                            foreach (var realm in result.Realms)
                                realm.Region = region; 
                            return result.Realms;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Err(ex.ToString());
                    return null;
                }
            });
        }

        string BuildWowRealmStatusApiUrl(string regionalUrl, IEnumerable<CharacterProfile> profiles)
        {
            var serverList = new List<string>();
            foreach (var profile in profiles)
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
            return regionalUrl + ret;
        }

        [DataContract]
        public class WowRealmStatusEntry
        {
            public WowSettings.WowRegion Region { get; internal set; }
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
