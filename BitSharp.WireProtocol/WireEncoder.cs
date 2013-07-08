using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.WireProtocol
{
    internal class WireEncoder
    {
        public static byte[] WireEncodeData<T>(T item, Action<WireWriter, T> wireEncoder)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new WireWriter(stream);
                wireEncoder(writer, item);
                return stream.ToArray();
            }
        }

        public static T WireDecodeData<T>(byte[] data, Func<WireReader, T> wireDecoder)
        {
            using (var stream = new ByteArrayStream(data))
            {
                var reader = new WireReader(stream);
                return wireDecoder(reader);
            }
        }

        public static ImmutableArray<T> ReadList<T>(WireReader reader, Func<WireReader, T> wireDecoder)
        {
            var length = reader.ReadVarInt().ToIntChecked();

            var list = new T[length];
            for (var i = 0; i < length; i++)
            {
                list[i] = wireDecoder(reader);
            }

            return list.ToImmutableArray();
        }
    }
}
