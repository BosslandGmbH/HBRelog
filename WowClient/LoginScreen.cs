using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WowClient
{
    class LoginScreen : WowWrapper
    {
        public LoginScreen(WowWrapper wrapper)
            : base(wrapper)
        { }

        public bool IsValid { get; private set; }
        public bool IsBanned { get; private set; }
        public bool IsSuspended { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsSuspiciousLocked { get; private set; }
        public bool IsLockedLicense { get; private set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public IScreen DoLogin()
        {
            throw new NotImplementedException();
        }
    }
}
