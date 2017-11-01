using siof.Common.Extensions;
using siof.Common.Helpers.ReaderWriterLock;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace siof.Common.Wpf
{
    public class DispatchedObservableCollection<T>: NotifyObject, IList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        protected List<T> _collection;

        [NonSerialized]
        protected ReaderWriterLockSlim _collectionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public DispatchedObservableCollection(IEnumerable<T> col, Dispatcher dis = null)
        {
            CurrentDispatcher = dis ?? Dispatcher.CurrentDispatcher;
            CurrentDispatcherPriority = DispatcherPriority.Background;

            if (col != null)
                _collection = new List<T>(col);
            else
                _collection = new List<T>();
        }

        public DispatchedObservableCollection(Dispatcher dis = null) :
            this(null, dis)
        {
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public Dispatcher CurrentDispatcher { get; set; }

        public DispatcherPriority CurrentDispatcherPriority { get; set; }

        public char ToStringSeparator { get; set; }

        public int Count
        {
            get
            {
                using (new ReadOnlyLock(_collectionLock))
                {
                    return _collection.Count;
                }
            }
        }

        public bool HasItems
        {
            get
            {
                return Count > 0;
            }
        }

        public bool IsReadOnly { get { return false; } }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public object SyncRoot
        {
            get { return _collectionLock; }
        }

        public T this[int index]
        {
            get
            {
                using (new ReadOnlyLock(_collectionLock))
                {
                    T result = default(T);

                    if (index > -1 && _collection.Count > index)
                        result = _collection[index];

                    return result;
                }
            }

            set
            {
                using (new WriteLock(_collectionLock))
                {
                    if (index < 0 || _collection.Count <= index)
                        return;

                    _collection[index] = value;
                }
            }
        }

        object IList.this[int index]
        {
            get
            {
                return this[index];
            }

            set
            {
                if (value is T)
                {
                    this[index] = (T)value;
                }
#if DEBUG
                else
                {
                    throw new Exception(string.Format("Invalid item type!!!! Collection element type: {0}. Add element type {1}", typeof(T).Name, value != null ? value.GetType().Name : "NULL"));
                }
#endif
            }
        }

        public int BinarySearch(T value)
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                return _collection.BinarySearch(value);
            }
        }

        public void Sort(bool disableNotify = false)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Sort();

                if (!disableNotify)
                    NotifyReset();
            }
        }

        public void NotifyReset()
        {
            if (CollectionChanged == null)
                return;

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Add(T item)
        {
            InvokeIfNeeded(() => { DoAdd(item); });
        }

        public void AddRange(IEnumerable items)
        {
            InvokeIfNeeded(() => { DoAddRange(items); });
        }

        public void ReplaceAll(IEnumerable<T> newItems)
        {
            InvokeIfNeeded(() => DoReplaceAll(newItems, false));
        }

        public void AddRange(IEnumerable items, bool disableNotify)
        {
            InvokeIfNeeded(() => DoAddRange(items, disableNotify));
        }

        public DispatchedObservableCollection<T> AddRangeAndReturn(IEnumerable<T> items)
        {
            InvokeIfNeeded(() => DoAddRange(items));
            return this;
        }

        public void Clear()
        {
            InvokeIfNeeded(() => { DoClear(); });
        }

        public void ClearAndAdd(T item)
        {
            InvokeIfNeeded(() => DoClearAndAdd(item));
        }

        private void DoClearAndAdd(T item)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Clear();
                _collection.Add(item);
            }

            NotifyReset();
            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        public void ClearAndAddRange(IEnumerable items)
        {
            InvokeIfNeeded(() => DoClearAndAddRange(items));
        }

        public bool Contains(T item)
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                return _collection.Contains(item);
            }
        }

        public T Find(Predicate<T> pred)
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                return _collection.Find(pred);
            }
        }

        /// <summary>
        /// Pobiera listę elementów i tworzy ich kopię referencji
        /// </summary>
        /// <returns></returns>
        public T FirstOrDefault(Func<T, bool> filter)
        {
            T ret = default(T);
            using (new ReadLock(_collectionLock))
            {
                ret = _collection.FirstOrDefault(filter);
            }
            return ret;
        }

        public void ForEachSync(Action<T> action)
        {
            using (new ReadLock(_collectionLock))
            {
                _collection.ForEach(action);
            }
        }

        public IList<T> WhereSync(Func<T, bool> filter)
        {
            using (new ReadLock(_collectionLock))
            {
                return _collection.Where(filter).ToList();
            }
        }

        public IList<TResult> SelectSync<TResult>(Func<T, TResult> selector)
        {
            using (new ReadLock(_collectionLock))
            {
                return _collection.Select(selector).ToList();
            }
        }

        public IList<TResult> WhereSelectSync<TResult>(Func<T, bool> filter, Func<T, TResult> selector)
        {
            using (new ReadLock(_collectionLock))
            {
                return _collection.Where(filter).Select(selector).ToList();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.CopyTo(array, arrayIndex);
            }
        }

        public void Update(ICollection<T> updateCollection, Func<T, int> keyExpression)
        {
            InvokeIfNeeded(() => DoUpdate(updateCollection, keyExpression));
        }

        public bool Remove(T item)
        {
            return InvokeIfNeeded(() => DoRemove(item));
        }

        public void RemoveRange(IEnumerable removeCollection, bool disableNotify = false)
        {
            InvokeIfNeeded(() => DoRemoveRange(removeCollection, disableNotify));
        }

        public IEnumerator<T> GetEnumerator()
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                IList<T> itms = _collection.ToList();
                return itms.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                IList<T> itms = _collection.ToList();
                return itms.GetEnumerator();
            }
        }

        public int IndexOf(T item)
        {
            using (new ReadOnlyLock(_collectionLock))
            {
                return _collection.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            if (index < 0)
                index = 0;

            InvokeIfNeeded(() => DoInsert(index, item));
        }

        public T GetOrInsert(Func<T, bool> func, int index, T item)
        {
            if (index < 0)
                index = 0;

            return InvokeIfNeeded(() => DoGetOrInsert(func, index, item));
        }

        public void Move(T item, int newIndex)
        {
            InvokeIfNeeded(() => DoMove(item, newIndex));
        }

        public void Move(int oldIndex, int newIndex)
        {
            InvokeIfNeeded(() => DoMove(oldIndex, newIndex));
        }

        public void RemoveAt(int index)
        {
            InvokeIfNeeded(() => DoRemoveAt(index));
        }

        public void Replace(T oldItem, T newItem)
        {
            InvokeIfNeeded(() => DoReplace(oldItem, newItem));
        }

        public int Add(object value)
        {
            if (value is T)
            {
                Add((T)value);
                return Count - 1;
            }

#if DEBUG
            throw new Exception(string.Format("Invalid item type!!!! Collection element type: {0}. Add element type {1}", typeof(T).Name, value != null ? value.GetType().Name : "NULL"));
#endif
            return -1;
        }

        public bool Contains(object value)
        {
            if (value is T)
            {
                return Contains((T)value);
            }
            return false;
        }

        public int IndexOf(object value)
        {
            if (value is T)
            {
                return IndexOf((T)value);
            }
            return -1;
        }

        public void Insert(int index, object value)
        {
            if (value is T)
            {
                Insert(index, (T)value);
            }
#if DEBUG
            else
            {
                throw new Exception(string.Format("Invalid item type!!!! Collection element type: {0}. Insert element type {1}", typeof(T).Name, value != null ? value.GetType().Name : "NULL"));
            }
#endif
        }

        public void Remove(object value)
        {
            if (value is T)
            {
                Remove((T)value);
            }
        }

        public void CopyTo(Array array, int index)
        {
            if (array is T[])
            {
                CopyTo((T[])array, index);
            }
        }

        public override string ToString()
        {
            return InvokeIfNeeded(() => _collection.ToString(ToStringSeparator));
        }

        protected TT InvokeIfNeeded<TT>(Func<TT> func)
        {
            if (Thread.CurrentThread == CurrentDispatcher.Thread)
                return func();

            return DispatcherInvoke(func);
        }

        protected void InvokeIfNeeded(Action action)
        {
            if (Thread.CurrentThread == CurrentDispatcher.Thread)
                action();
            else
                DispatcherInvoke(action);
        }

        protected TT DispatcherInvoke<TT>(Func<TT> func)
        {
            return CurrentDispatcher.Invoke(func, CurrentDispatcherPriority);
        }

        protected object DispatcherInvoke(Action action)
        {
            return CurrentDispatcher.Invoke(CurrentDispatcherPriority, action);
        }

        protected void InvokeCollectionChanged(NotifyCollectionChangedEventArgs ev)
        {
            InvokeIfNeeded(() => DoCollectionChanged(ev));
        }

        private void DoAdd(T item)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Add(item);
            }

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoReplaceAll(IEnumerable<T> newItems, bool disableNotify)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Clear();
                _collection = new List<T>(newItems);
            }

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoAddRange(IEnumerable items, bool disableNotify = false)
        {
            List<T> itemsT;
            using (new WriteLock(_collectionLock))
            {
                itemsT = items.OfType<T>().ToList();
                foreach (var item in itemsT)
                {
                    _collection.Add(item);
                }
            }

            if (!disableNotify)
            {
                itemsT.ForEach(item => InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item)));
            }

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoClear()
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Clear();
            }

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoClearAndAddRange(IEnumerable items)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Clear();
                if (items != null)
                {
                    var itemsT = items.OfType<T>().ToList();
                    _collection.AddRange(itemsT);
                }
            }
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoUpdate(ICollection<T> updateCollection, Func<T, int> keyExpression)
        {
            List<NotifyCollectionChangedEventArgs> events = new List<NotifyCollectionChangedEventArgs>(updateCollection.Count);
            using (new WriteLock(_collectionLock))
            {
                foreach (var item in updateCollection)
                {
                    int key = keyExpression(item);
                    T oldItem = _collection.FirstOrDefault(en => keyExpression(en) == key);
                    if (oldItem == null)
                    {
                        _collection.Add(item);
                        events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                    }
                    else
                    {
                        if (oldItem.Equals(item) == false)
                        {
                            int oldItemIndex = _collection.IndexOf(oldItem);
                            if (oldItemIndex >= 0)
                            {
                                _collection[oldItemIndex] = item;
                                events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, oldItemIndex));
                            }
                        }
                    }
                }
            }
            if (CollectionChanged != null)
                events.ForEach(e => InvokeCollectionChanged(e));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private bool DoRemove(T item)
        {
            bool result = false;
            int index = -1;
            using (new WriteLock(_collectionLock))
            {
                index = _collection.IndexOf(item);
                if (index == -1)
                    return false;

                result = _collection.Remove(item);
            }

            if (result)
                InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
            return result;
        }

        private void DoRemoveRange(IEnumerable removeCollection, bool disableNotify = false)
        {
            List<T> itemsT = null;
            using (new WriteLock(_collectionLock))
            {
                itemsT = removeCollection.OfType<T>().Where(i => _collection.Contains(i)).ToList();
                _collection.RemoveRange(itemsT);
            }

            if (!disableNotify)
                itemsT.ForEach(item => InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item)));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoInsert(int index, T item)
        {
            using (new WriteLock(_collectionLock))
            {
                _collection.Insert(index, item);
            }

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private T DoGetOrInsert(Func<T, bool> func, int index, T item)
        {
            using (new WriteLock(_collectionLock))
            {
                T el = _collection.FirstOrDefault(func);
                if (el != null)
                    return el;

                _collection.Insert(index, item);
            }
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);

            return item;
        }

        private void DoMove(T item, int newIndex)
        {
            int oldIndex = -1;
            using (new WriteLock(_collectionLock))
            {
                oldIndex = _collection.IndexOf(item);
                _collection.RemoveAt(oldIndex);
                _collection.Insert(newIndex, item);
            }
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
        }

        private void DoMove(int oldIndex, int newIndex)
        {
            T item;
            using (new WriteLock(_collectionLock))
            {
                item = _collection[oldIndex];
                _collection.RemoveAt(oldIndex);
                _collection.Insert(newIndex, item);
            }
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
        }

        private void DoRemoveAt(int index)
        {
            T item;
            using (new WriteLock(_collectionLock))
            {
                if (_collection.Count == 0 || _collection.Count <= index)
                    return;

                item = _collection[index];
                _collection.RemoveAt(index);
            }

            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoReplace(T oldItem, T newItem)
        {
            int oldItemIndex = -1;
            using (new WriteLock(_collectionLock))
            {
                oldItemIndex = _collection.IndexOf(oldItem);
                if (oldItemIndex >= 0)
                    _collection[oldItemIndex] = newItem;
                else
                    _collection.Add(newItem);
            }

            if (oldItemIndex >= 0)
            {
                InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, oldItemIndex));
            }
            else
            {
                InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItem));
            }

            OnPropertyChanged(() => Count);
            OnPropertyChanged(() => HasItems);
        }

        private void DoCollectionChanged(NotifyCollectionChangedEventArgs ev)
        {
            try
            {
                CollectionChanged?.Invoke(this, ev);
            }
            catch (Exception)
            {
            }
        }
    }
}
