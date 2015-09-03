using System;

namespace Shared.Logging
{
    public interface ILoggerFactory
    {
        ILog Create(Type type);
    }
}