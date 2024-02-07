using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static JLUtility.Debug;

namespace JLUtility
{
    public static class Debug
    {
        public static bool DisableLog { get; set; } = false;
        public enum Flag
        {
            Normal,
            Warning,
            Error,
        }
        public static void Log(object value, Flag flag = Flag.Normal)
        {
            if (DisableLog)
            {
                return;
            }
            switch (flag)
            {
                case Flag.Normal:
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine(value);
                    break;
                case Flag.Warning:
                    System.Console.ForegroundColor = ConsoleColor.Yellow;
                    System.Console.WriteLine("WARNING::"+value);
                    break;
                case Flag.Error:
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine("ERROR::" + value);
                    throw new Exception("ERROR::" + value);
                default:
                    break;
            }
            System.Console.ForegroundColor = ConsoleColor.White;
        }
        public static void LogFormat(string str, params object[] value)
        {
            if (DisableLog)
            {
                return;
            }
            string final = str;
            System.Console.ForegroundColor = ConsoleColor.White;
            for (int i = 0; i < value.Length; i++)
            {
                final.Replace("{"+i+"}", value[i].ToString());
            }
            System.Console.WriteLine(final);
            System.Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
