using System;

namespace Flow.Launcher.Plugin.svgl
{
    /// <summary>
    /// Generic cache entry with expiration time
    /// </summary>
    /// <typeparam name="T">The type of data being cached</typeparam>
    public class CacheEntry<T>
    {
        /// <summary>
        /// The cached data
        /// </summary>
        public T Data { get; set; }
        
        /// <summary>
        /// When this cache entry was created
        /// </summary>
        public DateTime Created { get; set; }
        
        /// <summary>
        /// Creates a new cache entry with the current timestamp
        /// </summary>
        /// <param name="data">The data to cache</param>
        public CacheEntry(T data)
        {
            Data = data;
            Created = DateTime.Now;
        }
        
        /// <summary>
        /// Checks if the cache entry has expired based on the given lifetime in minutes
        /// </summary>
        /// <param name="lifetimeMinutes">Lifetime in minutes (0 means never expires)</param>
        /// <returns>True if expired, false otherwise</returns>
        public bool IsExpired(int lifetimeMinutes)
        {
            if (lifetimeMinutes <= 0)
                return false; // Never expires if lifetime is 0 or negative
                
            return DateTime.Now - Created > TimeSpan.FromMinutes(lifetimeMinutes);
        }
    }
}
