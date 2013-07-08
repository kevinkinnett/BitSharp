using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IDataStorage<TKey, TValue> : IReadOnlyDataStorage<TKey, TValue>
    {
        bool TryWriteValues(IEnumerable<KeyValuePair<TKey, WriteValue<TValue>>> values);
    }
}
