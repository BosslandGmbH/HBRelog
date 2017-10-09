﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;
using HighVoltz.HBRelog.WoW.Lua;
using Button = HighVoltz.HBRelog.WoW.FrameXml.Button;

namespace HighVoltz.HBRelog.WoW.States
{
	internal class RealmSelectState : State
	{
		private readonly WowManager _wowManager;
        private Stopwatch _realmSelectionTimer = new Stopwatch();
		public RealmSelectState(WowManager wowManager)
		{
			_wowManager = wowManager;
		}

		#region State Members

		public override int Priority
		{
			get { return 500; }
		}

		public override bool NeedToRun
		{
			get
			{
                return (_wowManager.GameProcess != null && !_wowManager.GameProcess.HasExitedSafe()) 
					&& !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && !_wowManager.IsConnectingOrLoading &&
					   _wowManager.GlueScreen == GlueScreen.RealmList;
			}
		}

		public override void Run()
		{
			if (_wowManager.Throttled)
				return;

			if (_wowManager.ServerHasQueue)
			{
				var status = QueueStatus;
				_wowManager.Profile.Status = string.IsNullOrEmpty(status) ? status : "Waiting in server queue";
				_wowManager.Profile.Log("Waiting in server queue");
                if (_wowManager.LockToken.IsValid)
                    _wowManager.LockToken.ReleaseLock();
                return;
			}

			if (_wowManager.IsConnectingOrLoading || IsConnecting)
				return;

			if (!IsRealmListVisible)
				return;

            // throttle how fast realm is selected.
		    if (_realmSelectionTimer.IsRunning && _realmSelectionTimer.ElapsedMilliseconds < 5000)
		        return;

		    if (!Utility.BringWindowIntoFocus(_wowManager.GameProcess.MainWindowHandle, _wowManager.Profile))
		        return;

			Utility.SaveForegroundWindowAndMouse();
			var tabs = RealmTabs;

            bool foundServer = false;

			if (tabs.Any())
			{

				while (tabs.Any() && _wowManager.GlueScreen == GlueScreen.RealmList)
				{
					foundServer = SelectRealm(_wowManager.Settings.ServerName);
					if (foundServer)
						break;
					var currentTab = tabs.FirstOrDefault(t => !t.IsEnabled);
					tabs.Remove(currentTab);
					var nextTab = tabs.FirstOrDefault();
					if (nextTab != null)
					{
						var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(nextTab);
						Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, true, false);
                        Utility.SleepUntil(() => !nextTab.IsEnabled, TimeSpan.FromMilliseconds(1000));
                        Thread.Sleep(1000);
					}
				}
			}
			else
			{
				foundServer = SelectRealm(_wowManager.Settings.ServerName);
			}

			Utility.RestoreForegroundWindowAndMouse();
		    if (!foundServer)
		    {
		        _wowManager.Profile.Log("Unable to find server. Pausing profile.");
		        _wowManager.Profile.Pause();
		    }
		    else
		    {
		        _realmSelectionTimer.Restart();
		    }
		}

        #endregion


        #region Properties

        private Button ScrollDownButton
		{
			get { return UIObject.GetUIObjectByName<Button>(_wowManager, "RealmListScrollFrameScrollBarScrollDownButton"); }
		}

		private Button ScrollUpButton
		{
			get { return UIObject.GetUIObjectByName<Button>(_wowManager, "RealmListScrollFrameScrollBarScrollUpButton"); }
		}

		private List<Button> RealmNameButtons
		{
			get
			{
			    const string groupName = "RealmListScrollFrameButton";
			    return UIObject.GetUIObjectsOfType<Button>(_wowManager)
			        .Where(b => b.IsVisible && b.Name.Contains(groupName) 
                        // Exclude buttons that are not on completely visible    
                        && b.Bottom >= ((Frame) b.Parent).Bottom).ToList();
			}
		}

		private string GlueDialogText
		{
			get
			{
				var glueDialogTextContol = UIObject.GetUIObjectByName<FontString>(_wowManager, "GlueDialogText");
				if (glueDialogTextContol != null && glueDialogTextContol.IsVisible)
					return glueDialogTextContol.Text;
				return string.Empty;
			}
		}

		private bool IsRealmListVisible
		{
			get
			{
				var realmList = UIObject.GetUIObjectByName<Frame>(_wowManager, "RealmList");
				return realmList != null && realmList.IsVisible;
			}
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

		private string QueueStatus
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

		private List<Button> RealmTabs
		{
			get
			{
				var list = new List<Button>();
				for (int i = 1; i <= 8; i++)
				{
					var tab = _wowManager.Globals.GetValue("RealmListTab" + i);
					if (tab == null) continue;
					var disabled = tab.Table.GetValue("disabled");
					if (disabled.Type == LuaType.Boolean && disabled.Boolean == true)
						continue;
					IntPtr lightUserDataPtr;
					if (UIObject.IsUIObject(tab.Table, out lightUserDataPtr))
					{
						var button = UIObject.GetUIObjectFromPointer<Button>(_wowManager, lightUserDataPtr);
						if (button != null && button.IsVisible)
							list.Add(button);
					}
				}
				return list;
			}
		}

		#endregion

		bool SelectRealm(string realm)
		{
            var scrollDownButton = ScrollDownButton;
			if (scrollDownButton == null)
				return false;

			var scrollUpButton = ScrollUpButton;
			if (scrollUpButton == null)
				return false;

			bool scrollUp = scrollUpButton.IsEnabled && scrollUpButton.IsVisible;

			for (int i = 0; i < 50; i++)
			{
				var realmButtons = RealmNameButtons;
				var wantedButton = realmButtons.FirstOrDefault(b => b.Text.Equals(realm, StringComparison.InvariantCultureIgnoreCase));
				PointF clickPos;
				if (wantedButton != null)
				{
					if (!wantedButton.IsEnabled)
					{
						_wowManager.Profile.Status = "Realm is offline";
						_wowManager.Profile.Log("Realm is offline");
						_wowManager.GameProcess.Kill();
						return true;
					}
					clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(wantedButton);
					Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, true, false);
                    Utility.SleepUntil(() => _wowManager.IsConnectingOrLoading, TimeSpan.FromMilliseconds(1000));
                    Thread.Sleep(2000);
					return true;
				}

				if (scrollUp && !scrollUpButton.IsEnabled)
					scrollUp = false;

				var scrollButton = scrollUp ? scrollUpButton : scrollDownButton;

				if (!scrollButton.IsEnabled || !scrollButton.IsVisible)
					break;


                clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(scrollButton);

				try
				{
                    for (int j = 0; j < 19; j++)
                    {
                        if (!scrollButton.IsEnabled)
                            break;
                        Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, false, false);
                    }
				}
				finally
				{
					NativeMethods.BlockInput(false);
				}
                Thread.Sleep(1000);
			}
			return false;
		}

	}
}