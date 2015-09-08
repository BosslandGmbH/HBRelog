using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WowClient.Lua.UI;

namespace WowClient
{
    public interface IScreen
    {
        UIObject FocusedWidget { get; }
        UIObject GetWidget(IAbsoluteAddress address);
        T GetWidget<T>(IAbsoluteAddress address) where T : UIObject;
        T GetWidget<T>(string name) where T : UIObject;
        IEnumerable<T> GetWidgets<T>() where T : UIObject;
        IEnumerable<UIObject> GetWidgets();
        IScreen Current { get; }
    }

    public interface ILoginScreen : IScreen
    {
        bool IsValid { get; }
        bool IsBanned { get; }
        bool IsSuspended { get; }
        bool IsFrozen { get; }
        bool IsSuspiciousLocked { get; }
        bool IsLockedLicense { get; }
        string Login { get; set; }
        string Password { get; set; }
        IScreen DoLogin();
    }
}
