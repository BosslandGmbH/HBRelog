using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            get { return !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && _wowManager.GlueStatus == WowManager.GlueState.Disconnected; }
        }

        public override void Run()
        {
            if (_wowManager.Throttled)
                return;

            if (_wowManager.StalledLogin)
            {
                _wowManager.Profile.Log("Failed to login wow, lets restart");
                _wowManager.GameProcess.Kill();
                return;
            }

            bool isBanned = IsBanned, isSuspended = IsSuspended, isFrozen = IsFrozen;

            if (isBanned || isSuspended || isFrozen)
            {
                string reason = isBanned ? "banned" : isSuspended ? "suspended" : "frozen";
                _wowManager.Profile.Status = string.Format("Account is {0}", reason);
                _wowManager.Profile.Log("Stoping profile because account is {0}.", reason);
                _wowManager.Profile.Stop();
                return;
            }

            //  press 'Enter' key if popup dialog with an 'Okay' button is visible
            if (IsErrorDialogVisible)
            {
                _wowManager.Profile.Log("Clicking okay on dialog.");
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
                return;
            }

            if (_wowManager.ServerHasQueue)
            {
                var status = QueueStatus;
                _wowManager.Profile.Status = string.IsNullOrEmpty(status) ? status : "Waiting in server queue";
                _wowManager.Profile.Log("Waiting in server queue");
                return;
            }

            // Select account from the account selection dialog.
            if (!HandleAccountSelectionDialog())
                return;

            if (_wowManager.IsConnectiongOrLoading || IsConnecting)
            {
                _wowManager.Profile.Log("Connecting...");
                return;
            }

            // enter Battlenet email..
            if (!EnterTextInEditBox("AccountLoginAccountEdit", _wowManager.Settings.Login))
                return;

            // enter password
            if (!EnterTextInEditBox("AccountLoginPasswordEdit", _wowManager.Settings.Password))
                return;

            // everything looks good. Press 'Enter' key to login.
            Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
        }


        private string _cancelText;
        bool IsConnecting
        {
            get
            {
                var dialogButtonText = GlueDialogButton1Text;
                if (string.IsNullOrEmpty(dialogButtonText))
                    return false;
                if (_cancelText == null)
                    _cancelText = _wowManager.Globals.GetValue("CANCEL").String.Value;
                return _cancelText == dialogButtonText;
            }
        }

        string QueueStatus
        {
            get
            {
                var dialogText = GlueDialogText;
                if (!string.IsNullOrEmpty(dialogText))
                {
                    var text = dialogText.Replace("\n", ". ");
                    return text;
                }
                return string.Empty;
            }
        }

        string GlueDialogTitle
        {
            get
            {
                var glueDialogTitleFontString = UIObject.GetUIObjectByName<FontString>(_wowManager, "GlueDialogTitle");
                if (glueDialogTitleFontString != null && glueDialogTitleFontString.IsVisible)
                    return glueDialogTitleFontString.Text;
                return string.Empty;
            }
        }

        string GlueDialogText
        {
            get
            {
                var glueDialogTextContol = UIObject.GetUIObjectByName<FontString>(_wowManager, "GlueDialogText");
                if (glueDialogTextContol != null && glueDialogTextContol.IsVisible)
                    return glueDialogTextContol.Text;
                return string.Empty;
            }
        }

        string GlueDialogButton1Text
        {
            get
            {
                var glueDialogButton1Text = UIObject.GetUIObjectByName<Button>(_wowManager, "GlueDialogButton1");
                if (glueDialogButton1Text != null && glueDialogButton1Text.IsVisible)
                    return glueDialogButton1Text.Text;
                return string.Empty;
            }
        }

        private const string BannedTitleText = "Battle.net Error #202";

        bool IsBanned
        {
            get
            {
                var dialogText = GlueDialogTitle;
                if (string.IsNullOrEmpty(dialogText))
                    return false;
                return BannedTitleText == dialogText;
            }
        }

        private const string SuspenedText = "Battle.net Error #203";

        bool IsSuspended
        {
            get
            {
                var dialogText = GlueDialogTitle;
                if (string.IsNullOrEmpty(dialogText))
                    return false;
                return SuspenedText == dialogText;
            }
        }

        private const string FrozenText = "Battle.net Error #206";

        bool IsFrozen
        {
            get
            {
                var dialogText = GlueDialogTitle;
                if (string.IsNullOrEmpty(dialogText))
                    return false;
                return FrozenText == dialogText;
            }
        }

        private string _okayText;
        bool IsErrorDialogVisible
        {
            get
            {
                var dialogButtonText = GlueDialogButton1Text;
                if (string.IsNullOrEmpty(dialogButtonText))
                    return false;
                if (_okayText == null)
                    _okayText = _wowManager.Globals.GetValue("OKAY").String.Value;
                return _okayText == dialogButtonText;
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
                    _wowManager.Profile.Log("Selecting Account");
                    Utility.SleepUntil(() => SelectedAccountIndex == buttonIndex, TimeSpan.FromSeconds(2));
                    return false;
                }
                _wowManager.Profile.Log("Accepting current account selection");
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            }
            return true;
        }

        int SelectedAccountIndex
        {
            get
            {
                return (int)_wowManager.Globals.GetValue("CURRENT_SELECTED_WOW_ACCOUNT").Value.Number;
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
                    _wowManager.Profile.Log("Pressing 'tab' key to gain set focus to {0}", editBoxName);
                    Utility.SleepUntil(() => editBox.HasFocus, TimeSpan.FromSeconds(2));
                    return false;
                }
                // check if we need to remove exisiting text.
                if (!string.IsNullOrEmpty(editBoxText))
                {
                    Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.End, false);
                    Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string('\b', editBoxText.Length * 2), false);
                    _wowManager.Profile.Log("Pressing 'end' + delete keys to remove contents from {0}", editBoxName);
                }
                Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, text);
                _wowManager.Profile.Log("Sending {0}letters to {1}", editBox.IsPassword ? "" : text.Length.ToString() + " ", editBoxName);
            }
            return true;
        }

    }
}
