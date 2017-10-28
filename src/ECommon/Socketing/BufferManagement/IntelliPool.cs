using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ECommon.Socketing.BufferManagement
{
    struct PoolItemState
    {
        public byte Generation { get; set; }
    }

    /// <summary>
    /// Intelligent object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IntelliPool<T> : IntelliPoolBase<T>
    {
        private ConcurrentDictionary<T, PoolItemState> _bufferDict = new ConcurrentDictionary<T, PoolItemState>();
        private ConcurrentDictionary<T, T> _removedItemDict;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntelliPool{T}"/> class.
        /// </summary>
        /// <param name="initialCount">The initial count.</param>
        /// <param name="itemCreator">The item creator.</param>
        /// <param name="itemCleaner">The item cleaner.</param>
        /// <param name="itemPreGet">The item pre get.</param>
        public IntelliPool(int initialCount, IPoolItemCreator<T> itemCreator, Action<T> itemCleaner = null, Action<T> itemPreGet = null)
            : base(initialCount, itemCreator, itemCleaner, itemPreGet)
        {

        }

        /// <summary>
        /// Registers the new item.
        /// </summary>
        /// <param name="item">The item.</param>
        protected override void RegisterNewItem(T item)
        {
            PoolItemState state = new PoolItemState();
            state.Generation = CurrentGeneration;
            _bufferDict.TryAdd(item, state);
        }

        /// <summary>
        /// Shrinks this instance.
        /// </summary>
        /// <returns></returns>
        public override bool Shrink()
        {
            var generation = CurrentGeneration;

            if (!base.Shrink())
                return false;

            var toBeRemoved = new List<T>(TotalCount / 2);

            foreach (var item in _bufferDict)
            {
                if (item.Value.Generation == generation)
                {
                    toBeRemoved.Add(item.Key);
                }
            }

            if (_removedItemDict == null)
                _removedItemDict = new ConcurrentDictionary<T, T>();

            foreach (var item in toBeRemoved)
            {
                PoolItemState state;
                if (_bufferDict.TryRemove(item, out state))
                    _removedItemDict.TryAdd(item, item);
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified item can be returned.
        /// </summary>
        /// <param name="item">The item to be returned.</param>
        /// <returns>
        ///   <c>true</c> if the specified item can be returned; otherwise, <c>false</c>.
        /// </returns>
        protected override bool CanReturn(T item)
        {
            return _bufferDict.ContainsKey(item);
        }

        /// <summary>
        /// Tries to remove the specific item
        /// </summary>
        /// <param name="item">The specific item to be removed.</param>
        /// <returns></returns>
        protected override bool TryRemove(T item)
        {
            if (_removedItemDict == null || _removedItemDict.Count == 0)
                return false;

            T removedItem;
            return _removedItemDict.TryRemove(item, out removedItem);
        }
    }

    /// <summary>
    /// Intelligent pool base class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class IntelliPoolBase<T> : IPool<T>
    {
        private ConcurrentStack<T> _store;
        private IPoolItemCreator<T> _itemCreator;
        private byte _currentGeneration = 0;
        private int _nextExpandThreshold;
        private int _totalCount;
        private int _availableCount;
        private int _inExpanding = 0;
        private Action<T> _itemCleaner;
        private Action<T> _itemPreGet;

        /// <summary>
        /// Gets the current generation.
        /// </summary>
        /// <value>
        /// The current generation.
        /// </value>
        protected byte CurrentGeneration
        {
            get { return _currentGeneration; }
        }
        /// <summary>
        /// Gets the total count.
        /// </summary>
        /// <value>
        /// The total count.
        /// </value>
        public int TotalCount
        {
            get { return _totalCount; }
        }
        /// <summary>
        /// Gets the available count, the items count which are available to be used.
        /// </summary>
        /// <value>
        /// The available count.
        /// </value>
        public int AvailableCount
        {
            get { return _availableCount; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntelliPoolBase{T}"/> class.
        /// </summary>
        /// <param name="initialCount">The initial count.</param>
        /// <param name="itemCreator">The item creator.</param>
        /// <param name="itemCleaner">The item cleaner.</param>
        /// <param name="itemPreGet">The item pre get.</param>
        public IntelliPoolBase(int initialCount, IPoolItemCreator<T> itemCreator, Action<T> itemCleaner = null, Action<T> itemPreGet = null)
        {
            _itemCreator = itemCreator;
            _itemCleaner = itemCleaner;
            _itemPreGet = itemPreGet;

            var list = new List<T>(initialCount);

            foreach (var item in itemCreator.Create(initialCount))
            {
                RegisterNewItem(item);
                list.Add(item);
            }

            _store = new ConcurrentStack<T>(list);

            _totalCount = initialCount;
            _availableCount = _totalCount;
            UpdateNextExpandThreshold();
        }

        /// <summary>
        /// Registers the new item.
        /// </summary>
        /// <param name="item">The item.</param>
        protected abstract void RegisterNewItem(T item);

        /// <summary>
        /// Gets an item from the pool.
        /// </summary>
        /// <returns></returns>
        public T Get()
        {
            T item;

            if (_store.TryPop(out item))
            {
                Interlocked.Decrement(ref _availableCount);

                if (_availableCount <= _nextExpandThreshold && _inExpanding == 0)
                    ThreadPool.QueueUserWorkItem(w => TryExpand());

                var itemPreGet = _itemPreGet;

                if (itemPreGet != null)
                    itemPreGet(item);

                return item;
            }

            //In expanding
            if (_inExpanding == 1)
            {
                var spinWait = new SpinWait();

                while (true)
                {
                    spinWait.SpinOnce();

                    if (_store.TryPop(out item))
                    {
                        Interlocked.Decrement(ref _availableCount);

                        var itemPreGet = _itemPreGet;

                        if (itemPreGet != null)
                            itemPreGet(item);

                        return item;
                    }

                    if (_inExpanding != 1)
                        return Get();
                }
            }
            else
            {
                TryExpand();
                return Get();
            }
        }

        bool TryExpand()
        {
            if (Interlocked.CompareExchange(ref _inExpanding, 1, 0) != 0)
                return false;

            Expand();
            _inExpanding = 0;
            return true;
        }

        void Expand()
        {
            var totalCount = _totalCount;

            foreach (var item in _itemCreator.Create(totalCount))
            {
                _store.Push(item);
                Interlocked.Increment(ref _availableCount);
                RegisterNewItem(item);
            }

            _currentGeneration++;

            _totalCount += totalCount;
            UpdateNextExpandThreshold();
        }

        /// <summary>
        /// Shrinks this pool.
        /// </summary>
        /// <returns></returns>
        public virtual bool Shrink()
        {
            var generation = _currentGeneration;
            if (generation == 0)
                return false;

            var shrinThreshold = _totalCount * 3 / 4;

            if (_availableCount <= shrinThreshold)
                return false;

            _currentGeneration = (byte)(generation - 1);
            return true;
        }

        /// <summary>
        /// Determines whether the specified item can be returned.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>
        ///   <c>true</c> if the specified item can be returned; otherwise, <c>false</c>.
        /// </returns>
        protected abstract bool CanReturn(T item);

        /// <summary>
        /// Tries to remove the specific item
        /// </summary>
        /// <param name="item">The specific item to be removed.</param>
        /// <returns></returns>
        protected abstract bool TryRemove(T item);

        /// <summary>
        /// Returns the specified item to the pool.
        /// </summary>
        /// <param name="item">The item to be returned.</param>
        public void Return(T item)
        {
            var itemCleaner = _itemCleaner;
            if (itemCleaner != null)
                itemCleaner(item);

            if (CanReturn(item))
            {
                _store.Push(item);
                Interlocked.Increment(ref _availableCount);
                return;
            }

            if (TryRemove(item))
                Interlocked.Decrement(ref _totalCount);
        }

        private void UpdateNextExpandThreshold()
        {
            _nextExpandThreshold = _totalCount / 5; //if only 20% buffer left, we can expand the buffer count
        }
    }
}
