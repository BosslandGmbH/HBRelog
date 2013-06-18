using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;
using Button = HighVoltz.HBRelog.WoW.FrameXml.Button;

namespace HighVoltz.HBRelog.WoW.States
{
    internal class CharacterSelectState : State
    {
        private readonly WowManager _wowManager;

        public CharacterSelectState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 500; }
        }

        public override bool NeedToRun
        {
            get { return !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && !_wowManager.IsConnectiongOrLoading && _wowManager.GlueStatus == WowManager.GlueState.CharacterSelection; }
        }

        public override void Run()
        {
            if (ShouldChangeRealm)
            {
                ChangeRealm();
                return;
            }
            HandleCharacterSelect();
        }

        bool HandleCharacterSelect()
        {
            // CharSelectCharacterButton5ButtonTextName
            const string groupName = "ButtonTextName";
            var characterNames = UIObject.GetUIObjectsOfType<FontString>(_wowManager).Where(b => b.IsVisible && b.Name.Contains(groupName))
                .OrderBy(fs => fs.Name)
                .Select(fs => fs.Text)
                .ToList();

            if (!characterNames.Any())
                return false;

            var charName = _wowManager.Profile.Settings.WowSettings.CharacterName;
            var wantedCharIndex =
                characterNames.FindIndex(n => string.Equals(n, charName, StringComparison.InvariantCultureIgnoreCase)) + 1;

            if (wantedCharIndex == 0)
            {
                _wowManager.Profile.Status = string.Format("Character name: {0} not found. Double check spelling", charName);
                _wowManager.Profile.Log("Character name not found. Double check spelling");
                return false;
            }
            
            // get current selected index from global variable CURRENT_SELECTED_WOW_ACCOUNT
            var currentIndex = SelectedCharacterIndex;

            if (wantedCharIndex != currentIndex)
            {
                if (wantedCharIndex > currentIndex)
                    Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string((char)Keys.Down, wantedCharIndex - currentIndex), false);
                else
                    Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string((char)Keys.Up, currentIndex - wantedCharIndex), false);
            }
            if (Utility.SleepUntil(() => SelectedCharacterIndex == wantedCharIndex, TimeSpan.FromSeconds(2)))
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            return true;
        }

        // 1-based.
        int SelectedCharacterIndex
        {
            get { return (int) _wowManager.Globals.GetValue("CharacterSelect").Table.GetValue("selectedIndex").Number; }
        }

        bool ShouldChangeRealm
        {
            get
            {
                var realmName = UIObject.GetUIObjectByName<FontString>(_wowManager, "AccountLoginRealmName");
                if (realmName == null || !realmName.IsVisible)
                    return false;
                return !string.Equals(realmName.Text, _wowManager.Profile.Settings.WowSettings.ServerName, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        readonly Stopwatch _realmChangeSw = new Stopwatch();
        void ChangeRealm()
        {
            if (_realmChangeSw.IsRunning && _realmChangeSw.Elapsed < TimeSpan.FromSeconds(5))
                return;
            var changeRealmButton = UIObject.GetUIObjectByName<Button>(_wowManager, "CharSelectChangeRealmButton");
            var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(changeRealmButton);
            Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y);
            _realmChangeSw.Restart();
        }

    }
}