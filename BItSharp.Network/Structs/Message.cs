using BitSharp.Common;
using BitSharp.Network.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using BitSharp.Common.ExtensionMethods;
using System.Diagnostics;
using System.IO;
using System.Collections.Immutable;

namespace BitSharp.Network
{
    public struct Message
    {
        public readonly UInt32 Magic;
        public readonly string Command;
        public readonly UInt32 PayloadSize;
        public readonly UInt32 PayloadChecksum;
        public readonly ImmutableArray<byte> Payload;

        public Message(UInt32 Magic, string Command, UInt32 PayloadSize, UInt32 PayloadChecksum, ImmutableArray<byte> Payload)
        {
            this.Magic = Magic;
            this.Command = Command;
            this.PayloadSize = PayloadSize;
            this.PayloadChecksum = PayloadChecksum;
            this.Payload = Payload;
        }

        public Message With(UInt32? Magic = null, string Command = null, UInt32? PayloadSize = null, UInt32? PayloadChecksum = null, ImmutableArray<byte>? Payload = null)
        {
            return new Message
            (
                Magic ?? this.Magic,
                Command ?? this.Command,
                PayloadSize ?? this.PayloadSize,
                PayloadChecksum ?? this.PayloadChecksum,
                Payload ?? this.Payload
            );
        }
    }
}
