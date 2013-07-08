using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IReadOnlyDataStorage<TKey, TValue>
    {
        IEnumerable<TKey> ReadAllKeys();

        IEnumerable<KeyValuePair<TKey, TValue>> ReadAllValues();

        bool TryReadValue(TKey key, out TValue value);
    }
}
