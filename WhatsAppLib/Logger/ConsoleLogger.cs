using System;
using System.Collections.Generic;
using System.Text;

namespace WhatsAppLib.Logger
{
    public class ConsoleLogger : ILogger
    {
        public void Debug(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-DEBUG--{log}");
        }

        public void Error(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-ERROR--{log}");
        }

        public void Fatal(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-FATAL--{log}");
        }

        public void Info(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-INFO--{log}");
        }

        public void Trace(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-TRACE--{log}");
        }

        public void Warn(string log)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}-WARN--{log}");
        }
    }
}
