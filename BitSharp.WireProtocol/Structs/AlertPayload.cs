using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct AlertPayload
    {
        public readonly string Payload;
        public readonly string Signature;

        public AlertPayload(string Payload, string Signature)
        {
            this.Payload = Payload;
            this.Signature = Signature;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Payload, Signature);
        }

        public AlertPayload With(string Payload = null, string Signature = null)
        {
            return new AlertPayload
            (
                Payload ?? this.Payload,
                Signature ?? this.Signature
            );
        }

        public static AlertPayload FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static AlertPayload ReadRawBytes(WireReader reader)
        {
            return new AlertPayload
            (
                Payload: reader.ReadVarString(),
                Signature: reader.ReadVarString()
            );
        }

        internal static byte[] ToRawBytes(string Payload, string Signature)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Payload, Signature);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, string Payload, string Signature)
        {
            writer.WriteVarString(Payload);
            writer.WriteVarString(Signature);
        }
    }
}
