using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;

namespace BitSharp.Node
{
    public static class Config
    {
        private static readonly string _localStoragePath;

        static Config()
        {
            _localStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp");
        }

        public static string LocalStoragePath { get { return _localStoragePath; } }
    }
}
