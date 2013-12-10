﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using HighVoltz.HBRelog.FiniteStateMachine;
using HighVoltz.HBRelog.WoW.FrameXml;
using Test.Lua;

namespace HighVoltz.HBRelog.WoW.States
{
	internal class RealmSelectState : State
	{
		private readonly WowManager _wowManager;
		private int _attempt = 1;
		public RealmSelectState(WowManager wowManager)
		{
			_wowManager = wowManager;
		}

		#region State Members

		public override int Priority
		{
			get { return 600; }
		}

		public override bool NeedToRun
		{
			get
			{
				return !_wowManager.StartupSequenceIsComplete && !_wowManager.InGame && !_wowManager.IsConnectiongOrLoading &&
					   _wowManager.GlueStatus == WowManager.GlueState.ServerSelection;
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
				return;
			}
			if (_wowManager.IsConnectiongOrLoading)
				return;

			if (!IsRealmListVisible)
				return;

			Utility.SaveForegroundWindowAndMouse();
			var tabs = RealmTabs;
			bool foundServer = false;

			if (tabs.Any())
			{

				while (tabs.Any() && _wowManager.GlueStatus == WowManager.GlueState.ServerSelection)
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
						// need to wait a little for click to register.
						try
						{
							NativeMethods.BlockInput(true);
							Utility.SleepUntil(() => !nextTab.IsEnabled, TimeSpan.FromMilliseconds(1000));
						}
						finally
						{
							NativeMethods.BlockInput(false);
						}
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
				if (_attempt < 4 && CancelRealmChange())
				{
					_wowManager.Profile.Log("Unable to find server on attempt #{0}. Canceling realm change.", _attempt);
					_attempt++;
				}
				else
				{
					_wowManager.Profile.Log("Unable to find server after attempt #{0}. Pausing profile.", _attempt);
					_wowManager.Profile.Pause();
					_attempt = 1;
				}
			}
			else
			{
				_attempt = 1;
			}
		}

		#endregion


		#region Properties

		private Button ScrollDownButton
		{
			get { return UIObject.GetUIObjectByName<Button>(_wowManager, "RealmListScrollFrameScrollBarScrollDownButton"); }
		}

		private List<Button> RealmNameButtons
		{
			get
			{
				const string groupName = "RealmListRealmButton";
				return UIObject.GetUIObjectsOfType<Button>(_wowManager).Where(b => b.IsVisible && b.Name.Contains(groupName)).ToList();
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
			for (int i = 0; i < 50; i++)
			{
				var realmButtons = RealmNameButtons;
				var wantedButton = realmButtons.FirstOrDefault(b => b.Text.Equals(realm, StringComparison.InvariantCultureIgnoreCase));
				PointF clickPos;
				if (wantedButton != null)
				{
					if (!wantedButton.IsEnabled)
					{
						_wowManager.Profile.Status = string.Format("Realm is offline");
						_wowManager.Profile.Log("Realm is offline");
						_wowManager.GameProcess.Kill();
						return true;
					}
					clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(wantedButton);
					Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, true, false);
					// need to wait a little for click to register.
					try
					{
						NativeMethods.BlockInput(true);
						Utility.SleepUntil(() => _wowManager.IsConnectiongOrLoading, TimeSpan.FromMilliseconds(1000));
					}
					finally
					{
						NativeMethods.BlockInput(false);
					}
					return true;
				}
				if (!scrollDownButton.IsEnabled || !scrollDownButton.IsVisible)
					break;
				clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(scrollDownButton);
				Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y, false, false);
				try
				{
					NativeMethods.BlockInput(true);
					Thread.Sleep(50);
				}
				finally
				{
					NativeMethods.BlockInput(false);
				}
			}
			return false;
		}

		bool CancelRealmChange()
		{
			var cancelButton = UIObject.GetUIObjectByName<Button>(_wowManager, "RealmListCancelButton");
			if (cancelButton == null || !cancelButton.IsVisible)
				return false;
			var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(cancelButton);
			Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int)clickPos.X, (int)clickPos.Y);
			return true;
		}

	}
}