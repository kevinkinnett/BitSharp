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
    public struct TxInput
    {
        private readonly TxOutputKey _previousTxOutputKey;
        private readonly ImmutableArray<byte> _scriptSignature;
        private readonly UInt32 _sequence;

        public TxInput(TxOutputKey previousTxOutputKey, ImmutableArray<byte> scriptSignature, UInt32 sequence)
        {
            this._previousTxOutputKey = previousTxOutputKey;
            this._scriptSignature = scriptSignature;
            this._sequence = sequence;
        }

        public TxOutputKey PreviousTxOutputKey { get { return this._previousTxOutputKey; } }

        public ImmutableArray<byte> ScriptSignature { get { return this._scriptSignature; } }

        public UInt32 Sequence { get { return this._sequence; } }

        public TxInput With(TxOutputKey? previousTxOutput = null, ImmutableArray<byte>? scriptSignature = null, UInt32? sequence = null)
        {
            return new TxInput
            (
                previousTxOutput ?? this.PreviousTxOutputKey,
                scriptSignature ?? this.ScriptSignature,
                sequence ?? this.Sequence
            );
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TxInput))
                return false;

            return (TxInput)obj == this;
        }

        public static bool operator ==(TxInput left, TxInput right)
        {
            return left.PreviousTxOutputKey == right.PreviousTxOutputKey && left.ScriptSignature.SequenceEqual(right.ScriptSignature) && left.Sequence == right.Sequence;
        }

        public static bool operator !=(TxInput left, TxInput right)
        {
            return !(left == right);
        }
    }
}
