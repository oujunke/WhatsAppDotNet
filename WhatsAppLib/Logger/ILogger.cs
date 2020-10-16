using System;
using System.Collections.Generic;
using System.Text;

namespace WhatsAppLib.Logger
{
    public interface ILogger
    {
        void Trace(string log);
        void Debug(string log);
        void Info(string log);
        void Warn(string log);
        void Error(string log);
        void Fatal(string log);
    }
}
