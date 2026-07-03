using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace DocGenPlatform.Tools
{
    ///<summary>
    ///通用线程安全过期缓存管理器（支持任意键/值类型，可自定义过期/排序规则）
    ///</summary>
    ///<typeparam name="TKey">字典键类型（如设备ID的string）</typeparam>
    ///<typeparam name="TValue">存储的元素类型（如EnvironFogModel）</typeparam>
    ///<remarks>
    ///初始化通用过期缓存管理器
    ///</remarks>
    ///<param name="isExpiredFunc">过期判断规则（必须：调用方定义元素是否过期）</param>
    public class ConcurrentExpireCache<TKey, TValue>(Func<TValue, bool> isExpiredFunc) : IDisposable
    {
        #region 私有字段（底层存储与线程安全控制）
        ///<summary>
        ///核心存储：键 → 元素列表
        ///</summary>
        private readonly ConcurrentDictionary<TKey, List<TValue>> _dataDict = new();

        ///<summary>
        ///读写锁字典：为每个列表单独维护读写锁（粒度更细）
        ///</summary>
        private readonly ConcurrentDictionary<TKey, ReaderWriterLockSlim> _listLocks = new();

        ///<summary>
        ///过期判断委托（由调用方自定义：元素是否过期）
        ///</summary>
        private readonly Func<TValue, bool> _isExpiredFunc = isExpiredFunc ?? throw new ArgumentNullException(nameof(isExpiredFunc), "必须提供过期判断规则");

        ///<summary>
        ///资源释放标志
        ///</summary>
        private bool _disposed = false;
        #endregion

        #region 通用核心方法（添加/查询/修改/清理）
        ///<summary>
        ///线程安全添加元素（支持单个/批量），注意该方法如果存在同一key则不进行添加直接返回，用于只命中一次的场景
        ///</summary>
        ///<param name="key">字典键（如设备ID）</param>
        ///<param name="values">要添加的元素（单个或多个）</param>
        public void Add(TKey key, params TValue[] values)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (values == null || values.Length == 0)
                return;

            //关键：创建新列表（先不添加到字典）
            var newList = new List<TValue>(values);
            //原子操作：仅当key不存在时，才添加新列表（返回是否添加成功）
            var added = _dataDict.TryAdd(key, newList);
            if (!added)
            {
                //添加失败（key已存在），直接返回
                return;
            }

            //若添加成功，同步创建对应的锁（确保锁与列表一一对应）
            _listLocks.TryAdd(key, new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion));
        }

        ///<summary>
        ///线程安全添加元素（支持单个/批量）
        ///</summary>
        ///<param name="key">字典键（如设备ID）</param>
        ///<param name="values">要添加的元素（单个或多个）</param>
        public void AddByKey(TKey key, params TValue[] values)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (values == null || values.Length == 0) return;

            //确保列表和锁存在（线程安全）
            var valueList = _dataDict.GetOrAdd(key, _ => new List<TValue>());
            var rwLock = _listLocks.GetOrAdd(key, _ => new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion));

            try
            {
                rwLock.EnterWriteLock();
                valueList.AddRange(values); //支持批量添加，比循环Add更高效
            }
            finally
            {
                if (rwLock.IsWriteLockHeld)
                    rwLock.ExitWriteLock();
            }
        }

        ///<summary>
        ///线程安全查询：获取指定键的元素（可自定义排序与数量）
        ///</summary>
        ///<param name="key">字典键（如设备ID）</param>
        ///<param name="filterFunc">查询条件（表达式）</param>
        ///<param name="orderByFunc">排序规则（可选：如按时间倒序，默认不排序）</param>
        ///<param name="takeCount">获取数量（可选：默认全部）</param>
        ///<returns>筛选后的元素列表（线程安全副本）</returns>
        public List<TValue> Get(TKey key, Func<TValue, bool> filterFunc = null, Func<IEnumerable<TValue>, IOrderedEnumerable<TValue>> orderByFunc = null, int takeCount = -1, bool IsDeepCopy = true)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            //键不存在时直接返回空列表
            if (!_dataDict.TryGetValue(key, out var valueList) || !_listLocks.TryGetValue(key, out var rwLock)) return [];

            var result = new List<TValue>();
            try
            {
                rwLock.EnterReadLock();
                //先获取列表副本（避免外部修改内部集合）
                var tempList = DeepCloneList(valueList, IsDeepCopy);
                //应用筛选条件
                if (filterFunc != null)
                    tempList = tempList.Where(filterFunc).ToList();
                //应用排序
                if (orderByFunc != null)
                    tempList = [.. orderByFunc(tempList)];
                //应用数量限制
                if (takeCount > 0 && tempList.Count > takeCount)
                    tempList = tempList.Take(takeCount).ToList();
                result = tempList;
            }
            finally
            {
                if (rwLock.IsReadLockHeld)
                    rwLock.ExitReadLock();
            }
            return result;
        }

        ///<summary>
        ///线程安全获取所有键中满足条件的数据（返回合并后的结果）
        ///</summary>
        ///<param name="filterFunc">全局筛选条件（可选：为null时返回所有数据）</param>
        ///<returns>所有满足条件的数据列表（线程安全副本）</returns>
        public List<TValue> GetAll(Func<TValue, bool> filterFunc = null, bool IsDeepCopy = true)
        {
            ThrowIfDisposed();

            var allResults = new List<TValue>();
            //1.获取当前所有键的快照（避免枚举时字典被修改导致异常）
            var allKeys = _dataDict.Keys.ToList();

            foreach (var key in allKeys)
            {
                //2.双重检查：确保键仍存在（其他线程可能已移除该键）
                if (!_dataDict.TryGetValue(key, out var valueList) ||
                    !_listLocks.TryGetValue(key, out var rwLock))
                    continue;

                try
                {
                    //3.获取读锁：确保读取期间列表不被修改
                    rwLock.EnterReadLock();

                    //4.对当前键的列表进行筛选（使用副本避免影响内部数据）
                    var keyData = DeepCloneList(valueList, IsDeepCopy);
                    if (filterFunc != null)
                        keyData = keyData.Where(filterFunc).ToList();

                    //5.合并到结果集（线程安全：结果集仅当前线程操作）
                    lock (allResults)
                    {
                        allResults.AddRange(keyData);
                    }
                }
                finally
                {
                    //6.确保读锁释放（无论是否出现异常）
                    if (rwLock.IsReadLockHeld)
                        rwLock.ExitReadLock();
                }
            }

            return allResults;
        }

        ///<summary>
        ///线程安全更新：按条件更新单个元素（找到第一个匹配元素后立即更新并返回）
        ///</summary>
        ///<param name="key">字典键（如设备ID）</param>
        ///<param name="matchFunc">匹配条件（定位要更新的元素）</param>
        ///<param name="updateFunc">更新逻辑（传入原元素，返回更新后的元素）</param>
        ///<returns>是否更新成功（true=找到并更新，false=未找到匹配元素）</returns>
        public bool UpdateSingle(TKey key, Func<TValue, bool> matchFunc, Func<TValue, TValue> updateFunc)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (matchFunc == null) throw new ArgumentNullException(nameof(matchFunc), "必须提供元素匹配条件");
            if (updateFunc == null) throw new ArgumentNullException(nameof(updateFunc), "必须提供元素更新逻辑");

            //键不存在时直接返回失败
            if (!_dataDict.TryGetValue(key, out var valueList) ||
                !_listLocks.TryGetValue(key, out var rwLock))
                return false;

            bool isUpdated = false;
            try
            {
                rwLock.EnterWriteLock();
                //找到第一个匹配的元素
                var targetIndex = valueList.FindIndex(item => matchFunc(item));
                if (targetIndex != -1)
                {
                    //执行更新逻辑，替换原元素
                    var updatedItem = updateFunc(valueList[targetIndex]);
                    valueList[targetIndex] = updatedItem;
                    isUpdated = true;
                }
            }
            finally
            {
                if (rwLock.IsWriteLockHeld)
                    rwLock.ExitWriteLock();
            }
            return isUpdated;
        }

        ///<summary>
        ///线程安全清理：根据指定Key，移除该Key下满足条件的数据（空列表自动清理）
        ///</summary>
        ///<param name="key">要清理的目标Key（如指定设备ID）</param>
        ///<param name="filterFunc">清理条件（必须：仅移除满足该条件的数据）</param>
        ///<returns>该Key下被清理的数据数量（-1表示Key不存在）</returns>
        public int CleanupByKey(TKey key, Func<TValue, bool> filterFunc = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key), "清理的目标Key不能为null");

            //1.检查Key是否存在，不存在直接返回-1
            if (!_dataDict.TryGetValue(key, out var valueList) ||
                !_listLocks.TryGetValue(key, out var rwLock))
                return -1;

            int removedCount = 0;
            try
            {
                rwLock.EnterWriteLock(); //写锁确保清理时数据不被修改

                //2.筛选出满足清理条件的数据
                if (filterFunc != null)
                {
                    var toRemoveItems = valueList.Where(filterFunc).ToList();
                    if (toRemoveItems.Count > 0)
                    {
                        //3.移除符合条件的数据（避免foreach中修改集合的异常）
                        foreach (var item in toRemoveItems)
                            valueList.Remove(item);
                        removedCount = toRemoveItems.Count;
                    }
                }
                else //清理key下全部
                {
                    removedCount = valueList.Count;
                    valueList.Clear();
                }

                //4.若列表为空，自动清理Key和对应的锁（防止内存泄漏）
                if (valueList.Count == 0)
                {
                    _dataDict.TryRemove(key, out _);
                    if (_listLocks.TryRemove(key, out var lockToDispose))
                    {
                        //提前释放当前写锁（避免锁对象释放时仍持有锁）
                        rwLock.ExitWriteLock();
                        //兜底释放锁资源（极端情况防护）
                        if (lockToDispose.IsReadLockHeld)
                            lockToDispose.ExitReadLock();
                        lockToDispose.Dispose();
                        return removedCount; //提前返回，避免后续重复释放锁
                    }
                }
            }
            finally
            {
                //5.确保写锁最终释放（仅当锁未被提前释放时）
                if (rwLock.IsWriteLockHeld)
                    rwLock.ExitWriteLock();
            }

            return removedCount;
        }

        ///<summary>
        ///线程安全清理：移除所有过期元素（空列表自动清理）
        ///</summary>
        ///<returns>清理的过期元素总数</returns>
        public int CleanupExpired(string ExpiredName = "")
        {
            ThrowIfDisposed();
            int totalRemoved = 0;
            var allKeys = _dataDict.Keys.ToList(); //避免枚举时字典变更

            foreach (var key in allKeys)
            {
                if (!_dataDict.TryGetValue(key, out var valueList) ||
                    !_listLocks.TryGetValue(key, out var rwLock))
                    continue;

                try
                {
                    rwLock.EnterWriteLock();
                    //1.筛选过期元素并移除
                    var expiredItems = valueList.Where(_isExpiredFunc).ToList();
                    if (expiredItems.Count > 0)
                    {
                        foreach (var item in expiredItems)
                            valueList.Remove(item);
                        totalRemoved += expiredItems.Count;
                    }

                    //2.列表为空时，清理字典和锁（避免内存泄漏）
                    if (valueList.Count == 0)
                    {
                        _dataDict.TryRemove(key, out _);
                        if (_listLocks.TryRemove(key, out var lockToDispose))
                        {
                            //操作完进行解锁
                            rwLock.ExitWriteLock();
                            //确保锁已完全释放（极端情况防护）
                            if (lockToDispose.IsReadLockHeld)
                                lockToDispose.ExitReadLock();
                            if (lockToDispose.IsWriteLockHeld)
                                lockToDispose.ExitWriteLock();
                            lockToDispose.Dispose();
                        }
                    }
                }
                finally
                {
                    if (rwLock.IsWriteLockHeld)
                        rwLock.ExitWriteLock();
                }
            }
            Console.WriteLine($"线程安全缓存中心数据：{ExpiredName}已清理");
            return totalRemoved;
        }

        ///<summary>
        ///清理缓存内存
        ///</summary>
        ///<returns></returns>
        public int Clear()
        {
            ThrowIfDisposed();
            int totalRemoved = 0;
            var allKeys = _dataDict.Keys.ToList(); //避免枚举时字典变更

            foreach (var key in allKeys)
            {
                if (!_dataDict.TryGetValue(key, out var valueList) ||
                    !_listLocks.TryGetValue(key, out var rwLock))
                    continue;

                try
                {
                    rwLock.EnterWriteLock();
                    //1.移除所有元素
                    totalRemoved = valueList.Count;
                    valueList.Clear();

                    //2.清理字典和锁（避免内存泄漏）
                    _dataDict.TryRemove(key, out _);
                    if (_listLocks.TryRemove(key, out var lockToDispose))
                    {
                        //操作完进行解锁
                        rwLock.ExitWriteLock();
                        //确保锁已完全释放（极端情况防护）
                        if (lockToDispose.IsReadLockHeld)
                            lockToDispose.ExitReadLock();
                        if (lockToDispose.IsWriteLockHeld)
                            lockToDispose.ExitWriteLock();
                        lockToDispose.Dispose();
                    }
                }
                finally
                {
                    if (rwLock.IsWriteLockHeld)
                        rwLock.ExitWriteLock();
                }
            }
            return totalRemoved;
        }
        #endregion

        #region 获取缓存数量
        public int GetCount()
        {
            ThrowIfDisposed();
            int total = 0;
            var keys = _dataDict.Keys.ToList();

            foreach (var key in keys)
            {
                if (_dataDict.TryGetValue(key, out var list) &&
                    _listLocks.TryGetValue(key, out var rwLock))
                {
                    try { rwLock.EnterReadLock(); total += list.Count; }
                    finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
                }
            }
            return total;
        }
        #endregion

        #region 深拷贝方法
        //自定义深拷贝方法（以JSON序列化为例，简单通用）
        private List<TValue> DeepCloneList(List<TValue> source, bool IsDeepCopy)
        {
            try
            {
                if (source == null || source.Count == 0)
                    return [];
                //需引用 Newtonsoft.Json 包
                if (IsDeepCopy)
                {
                    string json = JsonConvert.SerializeObject(source);
                    return JsonConvert.DeserializeObject<List<TValue>>(json) ?? [];
                }
                else
                    return new List<TValue>(source);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("深拷贝列表失败，可能是序列化过程中出现问题", ex);
            }
        }
        #endregion

        #region 尝试获取指定键的元素列表
        ///<summary>
        ///尝试获取指定键的元素列表（兼容FileSink调用）
        ///</summary>
        public bool TryGetValue(TKey key, out List<TValue>? value)
        {
            ThrowIfDisposed();
            if (key == null)
            {
                value = null;
                return false;
            }

            //复用已有的Get方法，线程安全
            var list = Get(key);
            value = list;
            return list != null && list.Count > 0;
        }
        #endregion

        #region 资源释放（IDisposable实现）
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); //告诉GC不需要执行Finalize
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            //1.释放托管资源（主动释放时执行）
            if (disposing)
            {
                //释放所有读写锁
                foreach (var rwLock in _listLocks.Values)
                    rwLock.Dispose();
                //清空字典
                _listLocks.Clear();
                _dataDict.Clear();
            }

            //2.释放非托管资源（如果有，此处无）
            _disposed = true;
        }

        ///<summary>
        ///释放后调用方法时抛出异常
        ///</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentExpireCache<TKey, TValue>), "缓存管理器已释放，无法执行操作");
        }

        //析构函数（仅在未主动Dispose时执行，作为兜底）
        ~ConcurrentExpireCache()
        {
            Dispose(false);
        }
        #endregion
    }
}
