using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.WoW
{
    class ConfigWtf
    {
        private const string ErrorMsg = @"Warning: Possible corrupt \WTF\Config.wtf file at line #:{0}./n/tReason: {1}";
        private string _path;
        private readonly WowManager _wowManager;
        readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        const StringComparison Comparer = StringComparison.InvariantCultureIgnoreCase;

        public ConfigWtf(WowManager wowManager, string path)
        {
            _wowManager = wowManager;
            _path = path;
            Load();
        }

        public bool Changed { get; private set; }

        public void EnsureValue(string key, string value)
        {
            if (_settings.ContainsKey(key) && string.Equals(_settings[key], value, Comparer))
                return;
            _settings[key] = value;
            Changed = true;
        }

        /// <summary>
        /// Ensures the name of the account is set as last used account name.. Use this rather than EnsureValue for setting account name
        /// </summary>
        /// <param name="value">The value.</param>
        public void EnsureAccountList(string value)
        {
            var selectedValue = "!" + value;
            if (_settings.ContainsKey("accountList"))
            {
                var accountNames = _settings["accountList"].Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                if (accountNames.Any(n => string.Equals(value, n, Comparer) || string.Equals(selectedValue, n, Comparer)))
                {
                    bool changed = false;
                    for (var i = 0; i < accountNames.Length; i++)
                    {
                        if (accountNames[i].StartsWith("!") && !string.Equals(accountNames[i], selectedValue, Comparer))
                        {
                            accountNames[i] = accountNames[i].Substring(1);
                            changed = true;
                        }
                        if (string.Equals(accountNames[i], value, Comparer))
                        {
                            accountNames[i] = "!" + accountNames[i];
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        _settings["accountList"] = accountNames.Aggregate((a, b) => a + "|" + b) + "|";
                        Changed = true;
                    }
                    return;
                }
            }
            _settings["accountList"] = selectedValue + "|";
            Changed = true;
        }


        /// <summary>
        /// Deletes the setting.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        public void DeleteSetting(string settingName)
        {
            if (!_settings.ContainsKey(settingName)) return;
            _settings.Remove(settingName);
            Changed = true;
        }

        private void Load()
        {
            var lines = File.ReadAllLines(_path);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNum = i + 1;
                var elements = line.Trim().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                // ensure there are 3 elements
                if (elements.Length != 3)
                {
                    // remove all white space.
                    var list = elements.Select(t => t.Trim()).ToList();
                    list.RemoveAll(string.IsNullOrWhiteSpace);
                    if ( list.Count > 0)
                    {
                        _wowManager.Profile.Log(ErrorMsg, lineNum, "Number of elements does not equal 3");
                    }
                    continue;
                }
                var settingName = elements[1];
                var rawSettingValue = elements[2];
                // ensure 1st element equals 'SET' case does not matter
                if (!string.Equals(elements[0], "SET", Comparer))
                {
                    _wowManager.Profile.Log(ErrorMsg, lineNum, "Missing 'SET'");
                    continue;
                }
                // ensure the 'value' (3rd) element is wrapped with double quotes
                if (rawSettingValue[0] != '"' || rawSettingValue[rawSettingValue.Length - 1] != '"')
                {
                    _wowManager.Profile.Log(ErrorMsg, lineNum, "Value not wrapped with double qoutes'");
                    continue;
                }
                if (_settings.ContainsKey(settingName))
                {
                    _wowManager.Profile.Log(ErrorMsg, lineNum, string.Format("{0} found multiple times", settingName));
                    continue;
                }
                var settingValue = rawSettingValue.Length <= 2 ? string.Empty : rawSettingValue.Substring(1, rawSettingValue.Length - 2);
                _settings.Add(settingName, settingValue);
            }
        }

        public void Save()
        {
            // make sure there's a backup copy before saving. 
            var configFolder = Path.GetDirectoryName(_path);
            var backupConfigPath = Path.Combine(configFolder, @"Config.wtf.bak");
            if (!File.Exists(backupConfigPath))
            {
                _wowManager.Profile.Log("Creating backup copy of Config.wtf");
                File.Copy(_path, backupConfigPath);
            }
            var sb = new StringBuilder(200);
            foreach (var setting in _settings)
            {
                sb.Append(string.Format("SET {0} \"{1}\"\n", setting.Key, setting.Value));
            }
            File.WriteAllText(_path, sb.ToString());
        }

    }
}
