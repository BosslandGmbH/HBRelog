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
            Utility.SaveForegroundWindowAndMouse();
            var tabs = RealmTabs;
            if (tabs.Any())
            {

                while (tabs.Any())
                {
                    if (SelectRealm(_wowManager.Settings.ServerName))
                        break;
                    var currentTab = tabs.FirstOrDefault(t => !t.IsEnabled);
                    tabs.Remove(currentTab);
                    var nextTab = tabs.FirstOrDefault();
                    if (nextTab != null)
                    {
                        var clickPos = _wowManager.ConvertWidgetCenterToWin32Coord(nextTab);
                        Utility.LeftClickAtPos(_wowManager.GameProcess.MainWindowHandle, (int) clickPos.X, (int) clickPos.Y, true, false);
                        // need to wait a little for click to register.
                        Thread.Sleep(500);
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
                SelectRealm(_wowManager.Settings.ServerName);
            }
            Utility.RestoreForegroundWindowAndMouse();
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
            while (true)
            {
                var realmButtons = RealmNameButtons;
                var wantedButton = realmButtons.FirstOrDefault(b => b.Text.Equals(realm, StringComparison.InvariantCultureIgnoreCase));
                PointF clickPos;
                if (wantedButton != null)
                {
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
            }
            return false;
        }


      
    }
}