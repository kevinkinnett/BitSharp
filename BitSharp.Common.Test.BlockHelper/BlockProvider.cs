using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.BlockHelper
{
    public abstract class BlockProvider
    {
        public abstract Block GetBlock(int index);

        public abstract Block GetBlock(string hash);

        public virtual IEnumerable<Block> GetBlocks(IEnumerable<int> blockIndexes)
        {
            var enumerator = blockIndexes.GetEnumerator();
            
            // move to the first block index
            if (!enumerator.MoveNext())
                yield break;

            // load up the first block to be returned
            var block = GetBlock(enumerator.Current);
            
            while (enumerator.MoveNext())
            {
                var nextBlockTask = Task.Run(() => GetBlock(enumerator.Current));
                yield return block;
                nextBlockTask.Wait();
                block = nextBlockTask.Result;
            }

            yield return block;
        }
    }
}
