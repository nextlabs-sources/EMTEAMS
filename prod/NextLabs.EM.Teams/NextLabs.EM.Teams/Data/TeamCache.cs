namespace NextLabs.Teams
{
	using System.Collections.Generic;
    using System.Threading;
    using System.Linq;
    using NextLabs.Teams.Models;

    public class TeamCache
    {
        private static readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
        private static Dictionary<string, CacheDetail> m_dicCache = new Dictionary<string, CacheDetail>();

        public TeamCache()
        {
        }

        public static void Init(List<TeamAttr> teamAttrs) 
        {
            try
            {
                rwLock.EnterWriteLock();

                foreach (var t in teamAttrs)
                {
                    m_dicCache[t.Id] = new CacheDetail(t.Name, t.Classifications, CacheStatus.SYNCED, t.DoEnforce);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static void CopyAllAndSetSYNC(out Dictionary<string, CacheDetail> dicWaitToPersist)
        {
            try
            {
                rwLock.EnterWriteLock();
                //Deep Clone
                dicWaitToPersist = m_dicCache.ToDictionary(entry => entry.Key, entry => (entry.Value.Clone() as CacheDetail));

                //Delete DELETED items
                var deletedItems = m_dicCache.Where(item => item.Value.Status == CacheStatus.DELETED).Select(item => item.Key).ToList();
                foreach (var d in deletedItems)  m_dicCache.Remove(d);

                foreach (var c in m_dicCache) 
                {
                    if(c.Value.Status != CacheStatus.SYNCED) c.Value.Status = CacheStatus.SYNCED;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static bool ContainKey(string key)
        {
            try
            {
                rwLock.EnterReadLock();
                bool result = false;
                result = m_dicCache.ContainsKey(key);
                if (result && m_dicCache[key].Status == CacheStatus.DELETED) result = false;
                return result;
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool TryGet(string key, out CacheDetail oVal)
        {
            try 
            {
                rwLock.EnterReadLock();
                var result = m_dicCache.TryGetValue(key, out oVal);
                if (result && oVal.Status == CacheStatus.DELETED) result = false;
                return result;
            }
            finally 
            { 
                rwLock.ExitReadLock(); 
            }
        }

        public static void SetAddOrUpdate(string key, Dictionary<string, List<string>> tags, string name, TeamEnforce enforce, bool overwrite = false)
        {
            if (tags == null) tags = new Dictionary<string, List<string>>();
            try
            {
                rwLock.EnterWriteLock();
                if (m_dicCache.ContainsKey(key))
                {
                    if (overwrite)
                    {
                        m_dicCache[key] = new CacheDetail(name, tags, CacheStatus.UPDATED, enforce);
                    }
                    else
                    {
                        if (tags.Count != 0) m_dicCache[key].AddOrUpdate(tags, enforce, name);
                        if (m_dicCache[key].Status == CacheStatus.DELETED) m_dicCache[key].Status = CacheStatus.ADDED;
                        else if (m_dicCache[key].Status == CacheStatus.SYNCED) m_dicCache[key].Status = CacheStatus.UPDATED;
                    }
                }
                else
                {
                    m_dicCache[key] = new CacheDetail(name, tags, CacheStatus.ADDED, enforce);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static void SetAddOrUpdate(string key, Dictionary<string, List<string>> tags, string name, TeamEnforce enforce, out Dictionary<string, List<string>> totalTags)
        {
            if (tags == null) tags = new Dictionary<string, List<string>>();
            try
            {
                rwLock.EnterWriteLock();
                if (m_dicCache.ContainsKey(key))
                {
                    if (tags.Count != 0) m_dicCache[key].AddOrUpdate(tags, enforce, name);
                    if (m_dicCache[key].Status == CacheStatus.DELETED) m_dicCache[key].Status = CacheStatus.ADDED;
                    else if (m_dicCache[key].Status == CacheStatus.SYNCED) m_dicCache[key].Status = CacheStatus.UPDATED;
                }
                else
                {
                    m_dicCache[key] = new CacheDetail(name, tags, CacheStatus.ADDED, enforce);
                }
                totalTags = new Dictionary<string, List<string>>(m_dicCache[key].Tags);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static void SetDelete(string key)
        {
            try
            {
                rwLock.EnterWriteLock();
                if (m_dicCache[key].Status != CacheStatus.DELETED) 
                {
                    if (m_dicCache[key].Tags != null && m_dicCache[key].Tags.Count != 0) m_dicCache[key].Tags.Clear();
                    m_dicCache[key].Status = CacheStatus.DELETED;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
    }
}
