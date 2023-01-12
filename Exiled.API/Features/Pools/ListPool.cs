// -----------------------------------------------------------------------
// <copyright file="ListPool.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.API.Features.Pools
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a system used to store and retrieve <see cref="List{T}"/> objects.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the list.</typeparam>
    /// <seealso cref="DictionaryPool{TKey, TValue}"/>
    /// <seealso cref="HashSetPool{T}"/>
    public class ListPool<T> : IPool<List<T>>
    {
        private readonly ConcurrentQueue<List<T>> pool = new();

        /// <summary>
        /// Gets a <see cref="ListPool{T}"/> that stores lists.
        /// </summary>
        public static ListPool<T> Pool { get; } = new();

        /// <inheritdoc/>
        public List<T> Get()
        {
            if (pool.TryDequeue(out List<T> list))
                return list;

            return new();
        }

        /// <summary>
        /// Retrieves a stored object of type <see cref="List{T}"/>, or creates it if it does not exist. The capacity of the list will be equal to or greater than <paramref name="capacity"/>.
        /// </summary>
        /// <param name="capacity">The capacity of content in the <see cref="List{T}"/>.</param>
        /// <returns>The stored object, or a new object, of type <see cref="List{T}"/>.</returns>
        public List<T> Get(int capacity)
        {
            if (pool.TryDequeue(out List<T> list))
            {
                list.EnsureCapacity(capacity);
                return list;
            }

            return new(capacity);
        }

        /// <summary>
        /// Retrieves a stored object of type <see cref="List{T}"/>, or creates it if it does not exist. The list will be filled with all the provided <paramref name="items"/>.
        /// </summary>
        /// <param name="items">The items to fill the list with.</param>
        /// <returns>The stored object, or a new object, of type <see cref="List{T}"/>.</returns>
        public List<T> Get(IEnumerable<T> items)
        {
            if (pool.TryDequeue(out List<T> list))
            {
                list.AddRange(items);
                return list;
            }

            return new(items);
        }

        /// <inheritdoc/>
        public void Return(List<T> obj)
        {
            obj.Clear();
            pool.Enqueue(obj);
        }

        /// <summary>
        /// Returns the <see cref="List{T}"/> to the pool and returns its contents as an array.
        /// </summary>
        /// <param name="obj">The <see cref="List{T}"/> to return.</param>
        /// <returns>The contents of the returned list as an array.</returns>
        public T[] ToArrayReturn(List<T> obj)
        {
            T[] array = obj.ToArray();

            Return(obj);

            return array;
        }
    }
}