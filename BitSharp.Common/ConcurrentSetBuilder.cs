using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ConcurrentSetBuilder<T> : IEnumerable<T>, ISet<T>, ICollection<T>
    {
        private readonly ImmutableHashSet<T>.Builder builder;
        //TODO make the set disposable because of lock?
        private readonly ReaderWriterLockSlim builderLock;

        public ConcurrentSetBuilder()
        {
            this.builder = ImmutableHashSet.CreateBuilder<T>();
            this.builderLock = new ReaderWriterLockSlim();
        }

        public bool Add(T item)
        {
            return this.builderLock.DoWrite(() =>
                this.builder.Add(item));
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            this.builderLock.DoWrite(() =>
                this.builder.ExceptWith(other));
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            this.builderLock.DoWrite(() =>
                this.builder.IntersectWith(other));
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.IsProperSubsetOf(other));
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.IsProperSupersetOf(other));
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.IsSubsetOf(other));
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.IsSupersetOf(other));
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.Overlaps(other));
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return this.builderLock.DoRead(() =>
                this.builder.SetEquals(other));
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            this.builderLock.DoWrite(() =>
                this.builder.SymmetricExceptWith(other));
        }

        public void UnionWith(IEnumerable<T> other)
        {
            this.builderLock.DoWrite(() =>
                this.builder.UnionWith(other));
        }

        void ICollection<T>.Add(T item)
        {
            this.Add(item);
        }

        public void Clear()
        {
            this.builderLock.DoWrite(() =>
                this.builder.Clear());
        }

        public bool Contains(T item)
        {
            return this.builderLock.DoRead(() =>
                this.builder.Contains(item));
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.builderLock.DoRead(() =>
                Buffer.BlockCopy(this.builder.ToArray(), 0, array, arrayIndex, this.builder.Count));
        }

        public int Count
        {
            get
            {
                return this.builderLock.DoRead(() =>
                    this.builder.Count);
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            return this.builderLock.DoWrite(() =>
                this.builder.Remove(item));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.ToImmutable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public ImmutableHashSet<T> ToImmutable()
        {
            return this.builderLock.DoRead(() =>
                this.builder.ToImmutable());
        }
    }
}
