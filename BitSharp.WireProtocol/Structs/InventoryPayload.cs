using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct InventoryPayload
    {
        public readonly ImmutableArray<InventoryVector> InventoryVectors;

        public InventoryPayload(ImmutableArray<InventoryVector> InventoryVectors)
        {
            this.InventoryVectors = InventoryVectors;
        }

        public InventoryPayload With(ImmutableArray<InventoryVector>? InventoryVectors = null)
        {
            return new InventoryPayload
            (
                InventoryVectors ?? this.InventoryVectors
            );
        }
    }
}
