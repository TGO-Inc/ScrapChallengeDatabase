using System.Collections;

namespace SteamWorkshop.WebAPI.Internal
{
    internal class ManagedArray<T> : IEnumerable<T>
    {
        public readonly int Size = 0;
        private readonly T[] base_array;
        public int Count { get; private set; } = 0;
        public ManagedArray(int length)
        {
            base_array = new T[length];
            Size = length;
        }
        public T this[int index]
        {
            get => base_array[index];
            set => base_array[index] = value;
        }
        public void Add(T item)
        {
            if (Count >= Size)
                throw new ArrayFullException();

            base_array[Count++] = item;
        }
        public void Add(T[] items)
        {
            if (Count + items.Length > Size)
                throw new ArrayFullException();

            items.CopyTo(base_array, Count);
            Count += items.Length;
        }
        public void Add(List<T> items)
        {
            if (Count + items.Count > Size)
                throw new ArrayFullException();

            items.CopyTo(base_array, Count);
            Count += items.Count;
        }
        public void Add(IEnumerable<T> items)
        {
            int length = items.Count();
            if (Count + length > Size)
                throw new ArrayFullException();
            foreach (var item in items)
                this.Add(item);
            Count += length;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return base_array[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public T[] ToArray()
        {
            return this.base_array;
        }
    }
    internal class ArrayFullException : Exception
    {
        public ArrayFullException()
            : base("The array has reached capacity")
        {
        }
        public ArrayFullException(string msg)
            : base(msg)
        {
        }
    }
}
