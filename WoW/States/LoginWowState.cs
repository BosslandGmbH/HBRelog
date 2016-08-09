using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;
using HighVoltz.HBRelog.WoW.Lua;
using WinAuth;
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

        public override bool NeedToRun => (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe())
                                          && !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame
                                          && _wowManager.GlueScreen == GlueScreen.Login;

	    public override void Run()
        {
	        if (_wowManager.Globals == null)
		        return;

            if (_wowManager.Throttled)
                return;

            if (_wowManager.StalledLogin)
            {
                _wowManager.Profile.Log("Failed to log into WoW; Restarting");
				_wowManager.LockToken.ReleaseLock();
				_wowManager.GameProcess.Kill();
				return;
            }


			bool isBanned = IsBanned, isSuspended = IsSuspended, isSuspiciousLocked = IsLockedSuspiciousActivity, isLockedLicense = IsLockedLicense;
            if (isBanned || isSuspended || isSuspiciousLocked || isLockedLicense)
            {
                string reason = isBanned ? "banned" : isSuspended ? "suspended" : isSuspiciousLocked ? "locked due to suspicious activity" : "locked license";

                _wowManager.Profile.Status = $"Account is {reason}";
                _wowManager.Profile.Log("Pausing profile because account is {0}.", reason);

				_wowManager.Profile.Pause();
                return;
            }

            if (IncorrectPassword)
            {
                _wowManager.Profile.Status = string.Format("Incorrect login information entered");
                _wowManager.Profile.Log("Pausing profile because incorrect login information was entered");
                _wowManager.Profile.Pause();
                return;
            }

            // Auto-click Okay on dialog
            if (ClickOkayButton())
                return;

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

            if (HandleBattleNetToken())
                return;

            // enter Battlenet email..
            if (EnterTextInEditBox("AccountLogin.UI.AccountEditBox", _wowManager.Settings.Login))
                return;

            // enter password
            if (EnterTextInEditBox("AccountLogin.UI.PasswordEditBox", _wowManager.Settings.Password))
                return;

            SetGameTitle();

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

        private string GlueDialogType
        {
            get
            {
                var glueDialog = UIObject.GetUIObjectByName<Frame>(_wowManager, "GlueDialog");

	            if (glueDialog != null && glueDialog.IsVisible)
	            {
		            var which = _wowManager.GetLuaObject("GlueDialog.which");
					if (which != null && !string.IsNullOrEmpty(which.String.Value))
						return which.String.Value;
				}
                return string.Empty;
            }
        }

		private string GlueDialogData
		{
			get
			{
				var glueDialog = UIObject.GetUIObjectByName<Frame>(_wowManager, "GlueDialog");

				if (glueDialog != null && glueDialog.IsVisible)
				{
					var data = _wowManager.GetLuaObject("GlueDialog.data");
					if (data != null && data.Pointer != IntPtr.Zero && data.Type != LuaType.Nil && !string.IsNullOrEmpty(data.String.Value))
						return data.String.Value;
				}
				return string.Empty;
			}
		}

		private string GlueDialogHtmlFormatText
		{
			get
			{
				var glueDialogHTML = UIObject.GetUIObjectByName<Frame>(_wowManager, "GlueDialogHTML");

				if (glueDialogHTML != null && glueDialogHTML.IsVisible)
					return glueDialogHTML.Regions.OfType<FontString>().FirstOrDefault()?.Text ?? "";
				return "";
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


        private const string GlueDialogData_IncorrectPassword = "WOW51900314";
        bool IncorrectPassword
        {
            get
            {
	            var dialogData = GlueDialogData;

                if (string.IsNullOrEmpty(dialogData))
                    return false;
	            return dialogData == GlueDialogData_IncorrectPassword;
            }
        }


		bool IsSuspended => IsGlueDialogHtmlVisible("BLZ51900053");

        private bool IsBanned => IsGlueDialogHtmlVisible("BLZ51900052");

        // ToDo: Implement IsLockedLicense
        bool IsLockedLicense => false;

        // ToDo: Implement IsLockedSuspiciousActivity
        bool IsLockedSuspiciousActivity => false;

		private bool IsGlueDialogHtmlVisible(string partialFormat)
		{
			var text = GlueDialogHtmlFormatText;
			if (string.IsNullOrEmpty(text))
				return false;
			return text.Contains(partialFormat);
		}

	    private string GetGlobalString(string str)
	    {
		    return _wowManager.Globals.GetValue(str)?.String.Value;
	    }


		private string _okayText;

        private bool ClickOkayButton()
        {
            if (_okayText == null)
                _okayText = _wowManager.Globals.GetValue("OKAY").String.Value;

            var buttons = GetGlueDialogButtons().ToList();
            var okayButton = buttons.FirstOrDefault(b => b.Text == _okayText);
            if (okayButton == null)
                return false;

            // if there's only one button then we just need to press 'Enter'
            if (buttons.Count == 1)
            {
                _wowManager.Profile.Log("Pressing 'Enter' key to close dialog.");
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            }
            else
            {
                _wowManager.Profile.Log("Clicking okay on dialog.");
                var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(okayButton);
                Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, true);
            }

            return true;
        }

        private static readonly string[] s_GlueDialogButtonNames = new[] { "GlueDialogButton1", "GlueDialogButton2", "GlueDialogButton3" };
        private IEnumerable<Button> GetGlueDialogButtons()
        {
            foreach (var buttonName in s_GlueDialogButtonNames)
            {
                var button = UIObject.GetUIObjectByName<Button>(_wowManager, buttonName);
                if (button != null && button.IsVisible)
                    yield return button;
            }
        }

        private bool HandleBattleNetToken()
        {
	        var serial = _wowManager.Settings.AuthenticatorSerial;
	        var restoreCode = _wowManager.Settings.AuthenticatorRestoreCode;

			if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(restoreCode)) 
				return false;

			if (string.IsNullOrEmpty(_wowManager.Settings.AuthenticatorSerial))
				return false;

			var frame = UIObject.GetUIObjectByName<Frame>(_wowManager, "AccountLogin.UI.TokenEntryDialog");
            if (frame == null || !frame.IsVisible || !frame.IsShown) return false;

            var editBox = UIObject.GetUIObjectByName<EditBox>(_wowManager, "AccountLogin.UI.TokenEntryDialog.Background.EditBox");

	        var auth = new BattleNetAuthenticator();

	        try
	        {
				auth.Restore(serial, restoreCode);
	        }
	        catch (Exception ex)
	        {
				_wowManager.Profile.Err("Could not get auth token: {0}", ex.Message);
		        return false;
	        }

            if (!string.IsNullOrEmpty(editBox.Text))
            {
                Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.End, false);
                Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string('\b', "AccountLoginTokenEdit".Length * 2), false);
                _wowManager.Profile.Log("Pressing 'end' + delete keys to remove contents from {0}", "AccountLoginTokenEdit");
            }

            Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, auth.CurrentCode);
            Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
            _wowManager.Profile.Log("Accepting Battle net token.");
            return true;
        }

        bool HandleAccountSelectionDialog()
        {
	        var accountContainer = UIObject.GetUIObjectByName<Frame>(_wowManager, "AccountLogin.UI.WoWAccountSelectDialog.Background.Container");
	        if (accountContainer == null)
		        return true;

			var accountButtons = accountContainer.Children.OfType<Button>().Where(b => b.IsVisible).ToList();
			if (!accountButtons.Any())
				return true;

            var wantedAccountButton =
                accountButtons.FirstOrDefault(b => string.Equals(b.Text, _wowManager.Settings.AcountName, StringComparison.InvariantCultureIgnoreCase));

            if (wantedAccountButton == null)
            {
                _wowManager.Profile.Log("Account name not found. Double check spelling");
                return false;
            }

            var buttonIndex = wantedAccountButton.Id;
            var currentIndex = SelectedAccountIndex;

			if (buttonIndex == currentIndex)
			{
				_wowManager.Profile.Log("Accepting current account selection");
				Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.Enter, false);
				return true;
			}

			_wowManager.Profile.Log("Selecting Account");
			var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(wantedAccountButton);
			Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, true);
            return true;
        }

        int SelectedAccountIndex
        {
            get
            {
                return (int)_wowManager.GetLuaObject("AccountLogin.UI.WoWAccountSelectDialog.selectedAccount").Value.Number;
            }
        }

        bool EnterTextInEditBox(string editBoxName, string text)
        {
            var editBox = UIObject.GetUIObjectByName<EditBox>(_wowManager, editBoxName);
            if (editBox == null || !editBox.IsVisible || !editBox.IsEnabled)
                return false;

            var editBoxText = editBox.Text;
	        if (editBox.MaxLetters > 0 && text.Length > editBox.MaxLetters)
		        text = text.Substring(0, editBox.MaxLetters);

            if (string.Equals(editBoxText, text, StringComparison.InvariantCultureIgnoreCase))
		        return false;

	        // do we have focus?
	        if (!editBox.HasFocus)
	        {
		        Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, "\t", false);
		        _wowManager.Profile.Log("Pressing 'tab' key to gain set focus to {0}", editBoxName);
		        Utility.SleepUntil(() => editBox.HasFocus, TimeSpan.FromSeconds(2));
		        return true;
	        }
	        // check if we need to remove exisiting text.
	        if (!string.IsNullOrEmpty(editBoxText))
	        {
		        Utility.SendBackgroundKey(_wowManager.GameProcess.MainWindowHandle, (char)Keys.End, false);
		        Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, new string('\b', editBoxText.Length * 2), false);
		        _wowManager.Profile.Log("Pressing 'end' + delete keys to remove contents from {0}", editBoxName);
	        }
	        Utility.SendBackgroundString(_wowManager.GameProcess.MainWindowHandle, text);
	        _wowManager.Profile.Log("Sending {0}letters to {1}", editBox.IsPassword ? "" : text.Length + " ", editBoxName);
	        return true;
        }

        private void SetGameTitle()
        {
            if (!HbRelogManager.Settings.SetGameWindowTitle) return;

            var title = HbRelogManager.Settings.GameWindowTitle;

            var profileNameI = title.IndexOf("{name}", StringComparison.InvariantCultureIgnoreCase);

            if (profileNameI >= 0)
                title = title.Replace(title.Substring(profileNameI, "{name}".Length),
                    _wowManager.Profile.Settings.ProfileName);

            var pidI = title.IndexOf("{pid}", StringComparison.InvariantCultureIgnoreCase);
            if (pidI >= 0)
                title = title.Replace(title.Substring(pidI, "{pid}".Length),
                    _wowManager.GameProcess.Id.ToString(CultureInfo.InvariantCulture));

            // change window title
            NativeMethods.SetWindowText(_wowManager.GameProcess.MainWindowHandle, title);
        }

    }
}
