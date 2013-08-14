using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;

namespace BitSharp.Common
{
    public class MethodTimer
    {
        public MethodTimer()
        {
            this.IsEnabled = true;
        }

        public MethodTimer(bool isEnabled)
        {
            this.IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; set; }

        public void Time(Action action, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Time(action, null, -1, memberName, lineNumber);
        }

        public void Time(string timerName, Action action, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Time(action, timerName, -1, memberName, lineNumber);
        }

        public void Time(long filterTime, Action action, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Time(action, null, filterTime, memberName, lineNumber);
        }

        public void Time(string timerName, long filterTime, Action action, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Time(action, timerName, filterTime, memberName, lineNumber);
        }

        public T Time<T>(Func<T> func, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            return Time(func, null, -1, memberName, lineNumber);
        }

        public T Time<T>(string timerName, Func<T> func, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            return Time(func, timerName, -1, memberName, lineNumber);
        }

        public T Time<T>(long filterTime, Func<T> func, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            return Time(func, null, filterTime, memberName, lineNumber);
        }

        public T Time<T>(string timerName, long filterTime, Func<T> func, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            return Time(func, timerName, filterTime, memberName, lineNumber);
        }

        private void Time(Action action, string timerName, long filterTime, string memberName, int lineNumber)
        {
            if (IsEnabled)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                action();

                stopwatch.Stop();
                WriteLine(stopwatch, timerName, filterTime, memberName, lineNumber);
            }
            else
            {
                action();
            }
        }

        private T Time<T>(Func<T> func, string timerName, long filterTime, string memberName, int lineNumber)
        {
            if (IsEnabled)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var result = func();

                stopwatch.Stop();
                WriteLine(stopwatch, timerName, filterTime, memberName, lineNumber);

                return result;
            }
            else
            {
                return func();
            }
        }

        private void WriteLine(Stopwatch stopwatch, string timerName, long filterTime, string memberName, int lineNumber)
        {
            if (IsEnabled)
            {
                if (timerName != null)
                    Debug.WriteLineIf(stopwatch.ElapsedMilliseconds > filterTime, "\t[TIMING] {0}:{1}:{2} took {3:#,##0.000000} s".Format2(timerName, memberName, lineNumber, stopwatch.ElapsedSecondsFloat()));
                else
                    Debug.WriteLineIf(stopwatch.ElapsedMilliseconds > filterTime, "\t[TIMING] {1}:{2} took {3:#,##0.000000} s".Format2(timerName, memberName, lineNumber, stopwatch.ElapsedSecondsFloat()));
            }
        }
    }
}
