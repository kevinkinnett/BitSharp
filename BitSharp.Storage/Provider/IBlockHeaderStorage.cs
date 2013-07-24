using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockHeaderStorage : IBoundedStorage<UInt256, BlockHeader>
    {
    }
}
