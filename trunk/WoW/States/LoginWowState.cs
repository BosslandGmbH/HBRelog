using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;
using Button = HighVoltz.HBRelog.WoW.FrameXml.Button;

namespace HighVoltz.HBRelog.WoW.States
{
    class LoginWowState : State
    {
        private readonly WowManager _wowManager;

        public LoginWowState(WowManager wowManager)
        {
            _wowManager = wowManager;
        }

        public override int Priority
        {
            get { return 700; }
        }

        public override bool NeedToRun
        {
            get { return !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && !_wowManager.IsConnectiongOrLoading && _wowManager.GlueStatus == WowManager.GlueState.Disconnected; }
        }

        public override void Run()
        {
            if (_wowManager.Throttled)
                return;
            if (!_wowManager.ServerIsOnline)
            {
                _wowManager.Profile.Status = "Waiting for server to come back online";
                return;
            }
            if (_wowManager.StalledLogin)
            {
                _wowManager.Profile.Log("Failed to login wow, lets restart");
                _wowManager.GameProcess.Kill();
                return;
            }

            //  press 'Enter' key if popup dialog with an 'Okay' button is visible
            if (IsErrorDialogVisible)
            {
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
                return;
            }

            // Select account from the account selection dialog.
            if (!HandleAccountSelectionDialog())
                return;

            if (IsConnecting)
                return;

            // enter Battlenet email..
            if (!EnterTextInEditBox("AccountLoginAccountEdit", _wowManager.Settings.Login))
                return;

            // enter password
            if (!EnterTextInEditBox("AccountLoginPasswordEdit", _wowManager.Settings.Password))
                return;

            var accountDropDownButton = UIObject.GetUIObjectByName<Button>(_wowManager, "AccountLoginDropDownButton");
            // everything looks good. Press 'Enter' key to login.
            Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
        }

        bool IsConnecting
        {
            get
            {
                var glueDialogButton1 = UIObject.GetUIObjectByName<Button>(_wowManager, "GlueDialogButton1");
                if (glueDialogButton1 != null && glueDialogButton1.IsVisible)
                {
                    // get localized 'Cancel' text.
                    var okayTextValue = _wowManager.Globals.GetValue("CANCEL");
                    if (okayTextValue != null)
                    {
                        if (glueDialogButton1.Text == okayTextValue.String.Value)
                            return true;
                    }
                }
                return false;
            }
        }

        bool IsErrorDialogVisible
        {
            get
            {
                var glueDialogButton1 = UIObject.GetUIObjectByName<Button>(_wowManager, "GlueDialogButton1");
                if (glueDialogButton1 != null && glueDialogButton1.IsVisible)
                {
                    // get localized 'Okay' text.
                    var okayTextValue = _wowManager.Globals.GetValue("OKAY");
                    if (okayTextValue != null)
                    {
                        if (glueDialogButton1.Text == okayTextValue.String.Value)
                            return true;
                    }
                }
                return false;
            }
        }

        bool HandleAccountSelectionDialog()
        {
            const string buttonGroupName = "WoWAccountSelectDialogBackgroundContainerButton";
            var accountButtons = UIObject.GetUIObjectsOfType<Button>(_wowManager).Where(b => b.IsVisible && b.Name.Contains(buttonGroupName)).ToList();
            if (accountButtons.Any())
            {
                var wantedAccountButton =
                    accountButtons.FirstOrDefault(b => string.Equals(b.Text, _wowManager.Settings.AcountName, StringComparison.InvariantCultureIgnoreCase));
                if (wantedAccountButton == null)
                {
                    _wowManager.Profile.Log("Account name not found. Double check spelling");
                    return false;
                }
                var buttonIndex = wantedAccountButton.Id;

                var currentIndex = SelectedAccountIndex;

                if (buttonIndex != currentIndex)
                {
                    if (buttonIndex > currentIndex)
                        Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string((char)Keys.Down, buttonIndex - currentIndex), false);
                    else
                        Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string((char)Keys.Up, currentIndex - buttonIndex), false);
                }
                if (Utility.SleepUntil(() => SelectedAccountIndex == buttonIndex, TimeSpan.FromSeconds(2)))
                    Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            }
            return true;
        }

        int SelectedAccountIndex
        {
            get
            {
                for (int i = 1; i <= 8; i++)
                {
                    var highlightName = string.Format("WoWAccountSelectDialogBackgroundContainerButton{0}BGHighlight", i);
                    var tex = UIObject.GetUIObjectByName<Texture>(_wowManager, highlightName);
                    if (tex != null && tex.IsVisible)
                        return i;
                }
                return -1;
                //return (int)_wowManager.Globals.GetValue("CURRENT_SELECTED_WOW_ACCOUNT").Value.Number;
            }
        }

        bool EnterTextInEditBox(string editBoxName, string text)
        {
            var editBox = UIObject.GetUIObjectByName<EditBox>(_wowManager, editBoxName);
            if (editBox == null || !editBox.IsVisible || !editBox.IsEnabled)
                return false;

            var editBoxText = editBox.Text;
            if (!string.Equals(editBoxText, text, StringComparison.InvariantCultureIgnoreCase))
            {
                // do we have focus?
                if (!editBox.HasFocus)
                {
                    Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, "\t", false);
                    if (!Utility.SleepUntil(() => editBox.HasFocus, TimeSpan.FromSeconds(2)))
                        return false;
                }
                // check if we need to remove exisiting text.
                if (!string.IsNullOrEmpty(editBoxText))
                {
                    Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.End, false);
                    Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string('\b', editBoxText.Length * 2), false);
                }
                Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, text);
            }
            return true;
        }

    }
}
