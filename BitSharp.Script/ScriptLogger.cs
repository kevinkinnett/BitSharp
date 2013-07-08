using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Script;
using BitSharp.Common;
using BigIntegerBouncy = Org.BouncyCastle.Math.BigInteger;
using System.Numerics;
using System.Collections.Immutable;

namespace BitSharp.Script
{
    public class ScriptLogger : IDisposable
    {
        private static readonly bool TRACE_FILE = false;
        private static readonly bool TRACE_CONSOLE = false;
        private static readonly bool INFO_FILE = true;
        private static readonly bool INFO_CONSOLE = true;

        private StreamWriter writer;

        public ScriptLogger()
        {
            var file = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp/log/script.log"));
            if (!file.Directory.Exists)
                file.Directory.Create();

            this.writer = new StreamWriter(file.FullName, append: true, encoding: UTF8Encoding.UTF8);
        }

        public void Dispose()
        {
            writer.Dispose();
        }

        [Conditional("LOGGING")]
        public void LogTrace(string format, params object[] args)
        {
            Log("TRACE", TRACE_FILE, TRACE_CONSOLE, format, args);
        }

        [Conditional("LOGGING")]
        public void LogDebug(string format, params object[] args)
        {
            Log("DEBUG", TRACE_FILE, TRACE_CONSOLE, format, args);
        }

        [Conditional("LOGGING")]
        public void LogInfo(string format, params object[] args)
        {
            Log("INFO ", INFO_FILE, INFO_CONSOLE, format, args);
        }

        private void Log(string level, bool file, bool console, string format, params object[] args)
        {
            if (file || console)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i] is byte[])
                    {
                        args[i] = ((byte[])args[i]).ToHexDataString();
                    }
                    else if (args[i] is ImmutableArray<byte>)
                    {
                        args[i] = ((ImmutableArray<byte>)args[i]).ToHexDataString();
                    }
                    else if (args[i] is BigIntegerBouncy)
                    {
                        args[i] = ((BigIntegerBouncy)args[i]).ToByteArrayUnsigned().Reverse().ToHexNumberString();
                    }
                    else if (args[i] is BigInteger)
                    {
                        args[i] = ((BigInteger)args[i]).ToHexNumberString();
                    }
                    else if (args[i] is UInt128)
                    {
                        args[i] = ((UInt128)args[i]).ToHexNumberString();
                    }
                    else if (args[i] is UInt256)
                    {
                        args[i] = ((UInt256)args[i]).ToHexNumberString();
                    }
                }

                var value = string.Format(format, args);
                LogRaw(level, file, console, value);
            }
        }

        private void LogRaw(string level, bool file, bool debug, string value)
        {
            if (file || debug)
            {
                var timestampValue = string.Format("{0}\t{1}\t{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), level, value);

                if (debug)
                    Debug.WriteLine(value);
                if (file)
                {
                    writer.WriteLine(timestampValue);
                    writer.Flush();
                }
            }
        }
    }
}
