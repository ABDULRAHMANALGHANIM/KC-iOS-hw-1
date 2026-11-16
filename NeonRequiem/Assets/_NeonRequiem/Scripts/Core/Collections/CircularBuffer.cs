using System;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Fixed-capacity ring buffer. Pushing into a full buffer overwrites the oldest element, so
    /// memory use is bounded and steady-state pushes never allocate. Index 0 is the oldest element.
    /// Used for frame-time history and safe-position history; Memory Fracture will record into it.
    /// </summary>
    public sealed class CircularBuffer<T>
    {
        private readonly T[] m_Items;
        private int m_Head;
        private int m_Count;

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            m_Items = new T[capacity];
        }

        public int Capacity => m_Items.Length;

        public int Count => m_Count;

        public bool IsEmpty => m_Count == 0;

        public bool IsFull => m_Count == m_Items.Length;

        /// <summary>Element by age: 0 is the oldest, <c>Count - 1</c> the newest.</summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= m_Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return m_Items[(m_Head + index) % m_Items.Length];
            }
        }

        public T Newest
        {
            get
            {
                if (m_Count == 0)
                {
                    throw new InvalidOperationException("The buffer is empty.");
                }

                return this[m_Count - 1];
            }
        }

        public T Oldest
        {
            get
            {
                if (m_Count == 0)
                {
                    throw new InvalidOperationException("The buffer is empty.");
                }

                return m_Items[m_Head];
            }
        }

        /// <summary>Appends an element, overwriting the oldest one when full.</summary>
        /// <returns>True when an old element was overwritten.</returns>
        public bool PushBack(T item)
        {
            int capacity = m_Items.Length;
            if (m_Count == capacity)
            {
                m_Items[m_Head] = item;
                m_Head = (m_Head + 1) % capacity;
                return true;
            }

            m_Items[(m_Head + m_Count) % capacity] = item;
            m_Count++;
            return false;
        }

        /// <summary>Removes and returns the oldest element.</summary>
        public bool TryPopFront(out T item)
        {
            if (m_Count == 0)
            {
                item = default;
                return false;
            }

            item = m_Items[m_Head];
            m_Items[m_Head] = default;
            m_Head = (m_Head + 1) % m_Items.Length;
            m_Count--;
            return true;
        }

        /// <summary>Removes and returns the newest element.</summary>
        public bool TryPopBack(out T item)
        {
            if (m_Count == 0)
            {
                item = default;
                return false;
            }

            int index = (m_Head + m_Count - 1) % m_Items.Length;
            item = m_Items[index];
            m_Items[index] = default;
            m_Count--;
            return true;
        }

        public void Clear()
        {
            Array.Clear(m_Items, 0, m_Items.Length);
            m_Head = 0;
            m_Count = 0;
        }

        /// <summary>Copies elements oldest-first into <paramref name="destination"/>.</summary>
        /// <returns>The number of elements copied.</returns>
        public int CopyTo(T[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = Math.Min(m_Count, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = m_Items[(m_Head + i) % m_Items.Length];
            }

            return count;
        }
    }
}
