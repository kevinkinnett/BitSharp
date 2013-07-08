using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public enum DataType
    {
        Block,
        BlockHeader,
        BlockMetadata,
        Transaction
    }

    public class MissingDataException : Exception
    {
        private readonly DataType dataType;
        private readonly UInt256 dataKey;

        public MissingDataException(DataType dataType, UInt256 dataKey)
        {
            this.dataType = dataType;
            this.dataKey = dataKey;
        }

        public DataType DataType { get { return this.dataType; } }

        public UInt256 DataKey { get { return this.dataKey; } }
    }
}
