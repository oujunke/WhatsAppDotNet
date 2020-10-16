using System;
using System.Collections.Generic;
using System.Text;
using WhatsAppLib.Logger;

namespace WhatsAppLib.Utils
{
    public class LogUtil
    {
        public static ILogger Logger = new ConsoleLogger();
        public static void Trace(string log)
        {
            Logger.Trace(log);
        }
        public static void Debug(string log)
        {
            Logger.Debug(log);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public static void Info(string log)
        {
            Logger.Info(log);
        }
        public static void Warn(string log)
        {
            Logger.Warn(log);
        }
        public static void Error(string log)
        {
            Logger.Error(log);
        }

        public static void Fatal(string log)
        {
            Logger.Fatal(log);
        }
    }
}
