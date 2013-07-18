using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct TxOutput
    {
        private readonly UInt64 _value;
        private readonly ImmutableArray<byte> _scriptPublicKey;

        public TxOutput(UInt64 value, ImmutableArray<byte> scriptPublicKey)
        {
            this._value = value;
            this._scriptPublicKey = scriptPublicKey;
        }

        public UInt64 Value { get { return this._value; } }

        public ImmutableArray<byte> ScriptPublicKey { get { return this._scriptPublicKey; } }

        public TxOutput With(UInt64? Value = null, ImmutableArray<byte>? ScriptPublicKey = null)
        {
            return new TxOutput
            (
                Value ?? this.Value,
                ScriptPublicKey ?? this.ScriptPublicKey
            );
        }
    }
}
