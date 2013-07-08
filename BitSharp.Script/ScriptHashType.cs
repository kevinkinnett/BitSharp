using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Script
{
    public enum ScriptHashType
    {
        SIGHASH_ALL = 1,
        SIGHASH_NONE = 2,
        SIGHASH_SINGLE = 3,
        SIGHASH_ANYONECANPAY = 0x80
    }
}
