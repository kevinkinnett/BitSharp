using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using BitSharp.Common.ExtensionMethods;
using System.Diagnostics;
using System.IO;
using System.Collections.Immutable;

namespace BitSharp.WireProtocol
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

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Magic, Command, PayloadSize, PayloadChecksum, Payload);
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

        public static Message FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static Message ReadRawBytes(WireReader reader)
        {
            var magic = reader.Read4Bytes();
            var command = reader.ReadFixedString(12);
            var payloadSize = reader.Read4Bytes();
            var payloadChecksum = reader.Read4Bytes();
            var payload = reader.ReadRawBytes(payloadSize.ToIntChecked()).ToImmutableArray();

            return new Message
            (
                Magic: magic,
                Command: command,
                PayloadSize: payloadSize,
                PayloadChecksum: payloadChecksum,
                Payload: payload
            );
        }

        internal static byte[] ToRawBytes(UInt32 Magic, string Command, UInt32 PayloadSize, UInt32 PayloadChecksum, ImmutableArray<byte> Payload)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Magic, Command, PayloadSize, PayloadChecksum, Payload);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 Magic, string Command, UInt32 PayloadSize, UInt32 PayloadChecksum, ImmutableArray<byte> Payload)
        {
            writer.Write4Bytes(Magic);
            writer.WriteFixedString(12, Command);
            writer.Write4Bytes(PayloadSize);
            writer.Write4Bytes(PayloadChecksum);
            writer.WriteRawBytes(PayloadSize.ToIntChecked(), Payload.ToArray());
        }
    }
}
