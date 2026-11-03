namespace Data.Interfaces
{
    public interface ICacheService
    {
        Task ObjectToCache(string key,object itemToCache,TimeSpan timetolive);
        Task<T?> GetCachedObject<T>(string key);
        Task RemoveData(string key);
        Task RemoveByPrefixAsync(string prefix);
        Task<long> IncrementAsync(string key, TimeSpan timeToLive);
    }
}