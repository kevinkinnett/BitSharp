using BitSharp.Common;
using BitSharp.Network.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.Network
{
    public struct InventoryVector
    {
        public static readonly UInt32 TYPE_ERROR = 0;
        public static readonly UInt32 TYPE_MESSAGE_TRANSACTION = 1;
        public static readonly UInt32 TYPE_MESSAGE_BLOCK = 2;

        public readonly UInt32 Type;
        public readonly UInt256 Hash;

        public InventoryVector(UInt32 Type, UInt256 Hash)
        {
            this.Type = Type;
            this.Hash = Hash;
        }

        public InventoryVector With(UInt32? Type = null, UInt256? Hash = null)
        {
            return new InventoryVector
            (
                Type ?? this.Type,
                Hash ?? this.Hash
            );
        }

    }
}
