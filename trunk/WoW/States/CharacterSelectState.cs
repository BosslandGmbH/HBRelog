using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            get 
			{
                return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe()) 
				&& !_wowManager.StartupSequenceIsComplete 
				&& !_wowManager.InGame 
				&& !_wowManager.IsConnectiongOrLoading 
				&& _wowManager.GlueStatus == WowManager.GlueState.CharacterSelection; 
			}
        }

        public override void Run()
        {
            if (_wowManager.Throttled)
                return;
	        
			// trial account will have a promotion frame that requires clicking a 'Play Trial' button to enter game.
			if (ClickPlayTrial())
	        {
		        return;
	        }

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
            var characterNames = (from fontString in UIObject.GetUIObjectsOfType<FontString>(_wowManager)
                                  where fontString.IsVisible && fontString.Name.Contains(groupName)
                                  let parent = fontString.Parent as Frame
                                  where parent != null
                                  let grandParent = parent.Parent as Button
                                  where grandParent != null
                                  orderby grandParent.Id
                                  select fontString.Text).ToList();

            if (!characterNames.Any())
                return false;

            var charName = _wowManager.Settings.CharacterName;
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
                Utility.SleepUntil(() => SelectedCharacterIndex == wantedCharIndex, TimeSpan.FromSeconds(2));
                return false;
            }
            Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            return true;
        }

        // 1-based.
        int SelectedCharacterIndex
        {
            get { return (int)_wowManager.Globals.GetValue("CharacterSelect").Table.GetValue("selectedIndex").Number; }
        }

        bool ShouldChangeRealm
        {
            get
            {
                var realmName = CurrentRealmName;
                if (string.IsNullOrEmpty(realmName))
                    return false;
                return !realmName.ToLowerInvariant().Contains(_wowManager.Settings.ServerName.ToLowerInvariant());
            }
        }

        string CurrentRealmName
        {
            get
            {
                var realmName = UIObject.GetUIObjectByName<FontString>(_wowManager, "CharSelectRealmName");
                return realmName != null ? realmName.Text.Trim() : string.Empty;
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
            _wowManager.Profile.Log("Changing server.");
            _realmChangeSw.Restart();
        }

	    bool ClickPlayTrial()
	    {
		    var promotionFrame = UIObject.GetUIObjectByName<Frame>(_wowManager, "PromotionFrame");
			if (promotionFrame == null || !promotionFrame.IsVisible)
				return false;

		    var playButton = promotionFrame.Children.LastOrDefault() as Button;
		    if (playButton == null)
		    {
			    Log.Write("Unable to find the 'Play Trial' button! notify developer");
				_wowManager.Profile.Pause();
				return false;
		    }
			var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(playButton);
			Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y);
			return true;
	    }
    }
}