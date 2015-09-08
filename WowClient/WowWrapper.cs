using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WowClient.Lua;
using WowClient.Lua.UI;
using Shared;

namespace WowClient
{
    public class WowWrapper : IScreen, IDisposable
    {
        public WowWrapper()
        {}

        public void AttachTo(Process process)
        {
            // TODO check process is valid
            try
            {
                WowProcess = process;
                Memory = new ReadOnlyMemory(process);
                GameStateAddress = Memory.FindPattern(WowPatterns.GameStatePattern).Deref(2);
                LuaStateAddress = Memory.FindPattern(WowPatterns.LuaStatePattern)
                    .Deref(2)
                    .Deref();
                // it is actually a reference to where address to focused widget is located
                // so to get address of focused widget we should deref this reference
                FocusedWidgetAddressRef = Memory.FindPattern(WowPatterns.FocusedWidgetPattern).Deref(2);
                LoadingScreenEnableCountAddress = Memory.FindPattern(WowPatterns.LoadingScreenEnableCountPattern).Deref(2);
                GlueStateAddress = Memory.FindPattern(WowPatterns.GlueStatePattern).Deref(2);
            }
            catch (Exception e)
            {
                throw new Exception("Could not attach WowWrapper.", e);
            }
        }

        public WowWrapper(WowWrapper wrapper)
        {
            try
            {
                WowProcess = wrapper.WowProcess;
                Memory = wrapper.Memory;
                GameStateAddress = wrapper.GameStateAddress;
                LuaStateAddress = wrapper.LuaStateAddress;
                FocusedWidgetAddressRef = wrapper.FocusedWidgetAddressRef;
                LoadingScreenEnableCountAddress = wrapper.LoadingScreenEnableCountAddress;
                GlueStateAddress = wrapper.GlueStateAddress;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                throw new Exception("Could not initialize WowWrapper.");
            }
        }

        public Process WowProcess { get; private set; }
        private IAbsoluteAddress GameStateAddress { get; set; }
        private IAbsoluteAddress LuaStateAddress { get; set; }

        private IAbsoluteAddress FocusedWidgetAddressRef { get; set; }
        public IAbsoluteAddress FocusedWidgetAddress {
            get { return FocusedWidgetAddressRef.Deref(); } }
        private IAbsoluteAddress LoadingScreenEnableCountAddress { get; set; }

        private IAbsoluteAddress GlueStateAddress { get; set; }

        internal const int LuaStateGlobalsOffset = 0x50;
        public IReadOnlyMemory Memory { get; set; }
        public string ActiveCharacterName { get; set; }

        public UIObject FocusedWidget
        {
            get
            {
                return GetWidget(FocusedWidgetAddress);
            }
        }

        public GlueState CurrentGlueState
        {
            get
            {
                return (GlueState)GlueStateAddress.Deref<int>();
            }
        }

        private LuaTable _globals;
        public LuaTable Globals
        {
            get
            {
                if (Memory == null)
                    return null;
                if (LuaStateAddress.Value == IntPtr.Zero)
                    return null;
                var globalsAddress = LuaStateAddress.Deref(LuaStateGlobalsOffset);
                if (globalsAddress.Value == IntPtr.Zero)
                    return null;
                if (_globals == null || !_globals.Address.Equals(globalsAddress))
                    _globals = new LuaTable(Memory, globalsAddress);
                return _globals;
            }
            set { _globals = value; }
        }

        public void ResetGlobals()
        {
            LuaStateAddress = Memory.FindPattern(WowPatterns.LuaStatePattern)
                .Deref(2)
                .Deref();
            _globals = null;
        }

        public static class WowPatterns
        {
            public const string GlueStatePattern =
                "83 3d ?? ?? ?? ?? ?? 75 ?? e8 ?? ?? ?? ?? 8b 10 8b c8 ff 62 5c c3";
            public const string GameStatePattern =
                "80 3d ?? ?? ?? ?? ?? 74 ?? 50 b9 ?? ?? ?? ?? e8 ?? ?? ?? ?? 85 c0 74 ?? 8b 40 08 83 f8 02 74 ?? 83 f8 01 75 ?? b0 01 c3 32 c0 c3";
            // ref - FrameXML:EditBox:HasFocus
            public const string FocusedWidgetPattern =
                "3b 05 ?? ?? ?? ?? 0f 94 c1 51 ff 75 08 e8 ?? ?? ?? ?? 33 c0 83 c4 10 40 5d c3";
            // ref - Framescript_ExecuteBuffer.
            public const string LuaStatePattern =
                "8b 35 ?? ?? ?? ?? 33 db 57 3b c3 74 ?? 88 18 ff 75 08 8d 85 dc fe ff ff 68 ?? ?? ?? ?? 68 ?? ?? ?? ?? 50";
            // first offset used in 'LoadingScreenEnable' function. This function also fires the 'LOADING_SCREEN_ENABLED' Wrapper event.
            public const string LoadingScreenEnableCountPattern =
                "ff 05 ?? ?? ?? ?? 83 3d ?? ?? ?? ?? ?? 53 56 57 0f 8f ?? ?? ?? ?? 6a 00 e8 ?? ?? ?? ?? 59 e8 ?? ?? ?? ?? 84 c0 74 ?? 6a 00 68";
        }

        public void Dispose()
        {
            Memory.Dispose();
        }

        public UIObject GetWidget(IAbsoluteAddress address)
        {
            UIObject w = null;
            try
            {
                w = UIObject.Get(this, address);
            }
            catch (Exception e)
            {
                Console.WriteLine("cound not get widget, message: {0}", e);
            }
            while (w == null)
            {
                Task.Delay(100).Wait();
                ResetGlobals();
                try
                {
                    w = UIObject.Get(this, address);
                }
                catch (Exception)
                {
                    Console.WriteLine("cound not get widget, retry");
                    w = null;
                }
            }
            return w;
        }

        public T GetWidget<T>(IAbsoluteAddress address) where T : UIObject
        {
            return UIObject.Get<T>(this, address);
        }

        public T GetWidget<T>(string name) where T : UIObject
        {
            return UIObject.Get<T>(this, name);
        }

        public IEnumerable<T> GetWidgets<T>() where T : UIObject
        {
            return UIObject.GetAll<T>(this);
        }

        public IEnumerable<UIObject> GetWidgets()
        {
            return UIObject.GetAll(this);
        }

        private IScreen GetCurrnetScreen()
        {
            // determine state and return proper screen
            //return new LoginScreen(this);
            return null;
        }

        public IScreen Current {
            get
            {
                return GetCurrnetScreen();
            }
        }

        public async Task<bool> TypeIntoEditBoxAsync(string editBoxName, string text)
        {
            var editBox = GetWidget<EditBox>(editBoxName);

            if (editBox == null || !editBox.IsVisible || !editBox.IsEnabled)
            {
                // Console.WriteLine("editbox is not valid");
                return false;
            }

            if (editBox.Text == text)
            {
                // Console.WriteLine("editbox already got that text");
                return true;
            }

            if (!editBox.HasFocus && !await FocusEditBoxAsync(editBoxName))
            {
                // Console.WriteLine("cant focus");
                return false;
            }

            if (!string.IsNullOrEmpty(editBox.Text))
            {
                Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)Keys.End, false);
                Utility.SendBackgroundString(WowProcess.MainWindowHandle, new string('\b', editBox.Text.Length * 2), false);
            }

            if (!await Utility.WaitUntilAsync(() => string.IsNullOrEmpty(editBox.Text), TimeSpan.FromMilliseconds(500)))
            {
                // Console.WriteLine("cant clear text");
                return false;
            }

            Utility.SendBackgroundString(WowProcess.MainWindowHandle, text);

            if (!await Utility.WaitUntilAsync(() => editBox.Text == text, TimeSpan.FromMilliseconds(500)))
            {
                // Console.WriteLine("text verification fail");
                return false;
            }

            return true;
        }

        public async Task<bool> FocusEditBoxAsync(string editBoxName)
        {
            var eb = FocusedWidget;
            if (eb.Name == editBoxName)
                return true;
            await CycleEditBoxesAsync(editBoxName);
            var focus = FocusedWidget;
            return focus.Name == editBoxName;
        }

        public async Task<UIObject> NextEditBoxAsync()
        {
            var curr = FocusedWidget;
            // use async message post
            Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)Keys.Tab, false);
            var isNotTimeout = await Utility.WaitUntilAsync(() =>
                FocusedWidget != null && !FocusedWidget.Equals(curr), TimeSpan.FromMilliseconds(50));
            if (curr == null)
                return FocusedWidget != null && isNotTimeout ? FocusedWidget : null;
            return !curr.Equals(FocusedWidget) && isNotTimeout ? FocusedWidget : null;
        }

        public async Task<HashSet<UIObject>> CycleEditBoxesAsync(string editBoxName, int timeoutMilliseconds = 1000, int maxTries = 10)
        {
            var currFocus = FocusedWidget;
            var res = new HashSet<UIObject>();
            var t = Stopwatch.StartNew();
            var nextFocus = await NextEditBoxAsync();
            if (currFocus != null)
            {
                res.Add(currFocus);
                if (currFocus.Name == editBoxName)
                    return res;
            }
            var isNotTimeout = t.ElapsedMilliseconds < timeoutMilliseconds;
            while (isNotTimeout
                && nextFocus != null
                && !res.Contains(nextFocus)
                && res.Count < maxTries)
            {
                res.Add(nextFocus);
                if (nextFocus.Name == editBoxName) break;
                isNotTimeout = t.ElapsedMilliseconds < timeoutMilliseconds;
                nextFocus = await NextEditBoxAsync();
            }
            return res;
        }

        public bool IsLoginScreen()
        {
            var t = GetWidget<FontString>("GlueDialogText");
            return CurrentGlueState == GlueState.Disconnected
                && !IsInGame
                && (t.Equals(null) || !t.IsVisible);
        }

        public bool IsCharacterSelectionScreen()
        {
            if (IsInGame || IsConnectingOrLoading)
                return false;
            ResetGlobals();
            var t = GetWidget<FontString>("GlueDialogText");
            return CurrentGlueState == GlueState.CharacterSelection
                && (t.Equals(null) || !t.IsVisible);
        }

        public void SendKey(Keys key)
        {
            Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)key, false);
        }
        
        public void SendString(string str)
        {
            if (Regex.IsMatch(str, "^[^\t\b\n\r]*$"))
            {
                Utility.SendBackgroundString(WowProcess.MainWindowHandle, str);
            }
            else
            {
                Regex.Matches(str, "([^\t\b\n\r]*)([\t\b\n\r]*)")
                .Cast<Match>()
                .Select(m => new
                    {
                        noncontrolChars = m.Groups[1].Value,
                        controlChars = m.Groups[2].Value
                    })
                .Where(m => !string.IsNullOrEmpty(m.noncontrolChars) || !string.IsNullOrEmpty(m.controlChars))
                .ToList()
                .ForEach(m =>
                {
                    Utility.SendBackgroundString(WowProcess.MainWindowHandle, m.noncontrolChars);
                    Utility.SendBackgroundString(WowProcess.MainWindowHandle, m.controlChars, false);
                });
            }
        }

        public async Task<bool> SendChatAsync(string str)
        {
            var chk = new Func<bool>( () => FocusedWidget != null
                    && Regex.IsMatch(FocusedWidget.Name, "^ChatFrame\\d+EditBox$"));
            if (!chk())
            {
                SendKey(Keys.Enter);
                if (!await Utility.WaitUntilAsync(chk, 100))
                {
                    // timeout, try close full screen frames by sending Escs
                    SendKey(Keys.Escape);
                    SendKey(Keys.Enter);
                    if (!await Utility.WaitUntilAsync(chk, 100))
                    {
                        Console.WriteLine("can't open chat edit box");
                        return false;
                    }
                }
            }
            if (!await TypeIntoEditBoxAsync(FocusedWidget.Name, str))
            {
                Console.WriteLine("could not type into chat box");
                return false;
            }
            SendKey(Keys.Enter);
            if (!await Utility.WaitUntilAsync(() => !chk(), 100))
            {
                Console.WriteLine("could not send chat message, chat editbox did not closed for some reason");
                return false;
            }
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> Logout()
        {
            if (!await SendChatAsync("/logout"))
            {
                Console.WriteLine("could not send logout command");
                return false;
            }

            ResetGlobals();

            // transition to character select screen
            if (!await Utility.WaitUntilAsync(IsCharacterSelectionScreen, TimeSpan.FromSeconds(30), 100))
            {
                Console.WriteLine("unexpected screen, it should be character select screen");
                return false;
            }


            await Task.Delay(100);
            return true;
        }

        public string CurrentCharacterName
        {
            get
            {
                var w = GetWidget<FontString>("PlayerName");
                return (IsInGame && w != null) ? w.Text : null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public async Task<bool> GetIntoTheGameAsync(WowSettings settings)
        {
            if (settings == null)
                throw new ArgumentException("settings == null");
            if (!settings.IsValid())
                throw new ArgumentException("settings.IsValid() == false");
            var charName = CurrentCharacterName;
            if (charName != null && charName != settings.CharacterName)
            {
                if (!await Logout())
                {
                    Console.WriteLine("could not logout");
                    return false;
                }
            }
            if (IsLoginScreen())
            {
                // TODO factor out string constants
                if (!await TypeIntoEditBoxAsync("AccountLoginAccountEdit", settings.Login)
                    || !await TypeIntoEditBoxAsync("AccountLoginPasswordEdit", settings.Password))
                {
                    Console.WriteLine("can't enter credentials");
                    return false;
                }
                // transition to character select screen
                Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)Keys.Enter, false);
                if (!await Utility.WaitUntilAsync(IsCharacterSelectionScreen, TimeSpan.FromSeconds(30), 100))
                {
                    Console.WriteLine("connection timed out");
                    return false;
                }
            }
            if (IsCharacterSelectionScreen())
            {
                var activeCharNames = GetWidgets<FontString>()
                    .Where(w => w.IsVisible
                        && Regex.IsMatch(w.Name, "^CharSelectCharacterButton\\d+ButtonTextName")
                        // character name contains only word characters,
                        // colored text (like "text |...|r") means that character is inactive
                        && Regex.IsMatch(w.Text, "^\\w+$"))
                    .Select(w => w.Text.Split(' ')[0]).ToList();
                settings.AccountCharacterNames = activeCharNames;
                // TODO emit event account character names list was updated

                if (!activeCharNames.Contains(settings.CharacterName))
                {
                    Console.WriteLine("there's no character with specified name or it is inactive");
                    return false;
                }
                
                var selectedChar = GetWidget<FontString>("CharSelectCharacterName");
                while (selectedChar.Text != settings.CharacterName)
                {
                    var str = selectedChar.Text;
                    Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)Keys.Down, false);
                    await Utility.WaitUntilAsync(() => str != selectedChar.Text, 500, 100);
                }

                // transition into the game screen
                Utility.SendBackgroundKey(WowProcess.MainWindowHandle, (char)Keys.Enter, false);
                if (!await Utility.WaitUntilAsync(() => IsInGame, TimeSpan.FromMinutes(2), 100))
                {
                    Console.WriteLine("get into the game timed out");
                    return false;
                }
            }
            await Task.Delay(100);
            return IsInGame;
        }

        public bool IsInGame
        {
            get
            {
                try
                {
                    var state = GameStateAddress.Deref<byte>();
                    var loadingScreenCount = LoadingScreenEnableCountAddress.Deref<int>();
                    return state == 1 && loadingScreenCount == 0;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        public bool IsConnectingOrLoading
        {
            get
            {
                try
                {
                    return GameStateAddress.Deref<byte>(1) == 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        // wow process lifetime exit reason
        public enum ExitReason
        {
            Timeout,
            ProcessExited
        }
    }

    public enum GlueState
    {
        None = -1,
        Disconnected = 0,
        Updater,
        CharacterSelection = 2,
        CharacterCreation = 3,
        ServerSelection = 6,
        Credits = 7,
        RegionalSelection = 8
    }

}
