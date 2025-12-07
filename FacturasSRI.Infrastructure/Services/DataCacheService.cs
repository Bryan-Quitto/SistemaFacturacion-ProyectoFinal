using FacturasSRI.Application.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class DataCacheService
    {
        private readonly IMemoryCache _cache;
        private const string KeyCustomers = "KEY_CUSTOMERS";
        private const string KeyProducts = "KEY_PRODUCTS";

        public DataCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<List<CustomerDto>> GetCustomersAsync(Func<Task<List<CustomerDto>>> fallback)
        {
            return await GetOrSetAsync(KeyCustomers, fallback);
        }

        public async Task<List<ProductDto>> GetProductsAsync(Func<Task<List<ProductDto>>> fallback)
        {
            return await GetOrSetAsync(KeyProducts, fallback);
        }

        public void ClearCustomersCache()
        {
            _cache.Remove(KeyCustomers);
        }

        public void ClearProductsCache()
        {
            _cache.Remove(KeyProducts);
        }

        private async Task<List<T>> GetOrSetAsync<T>(string cacheKey, Func<Task<List<T>>> fallback) where T : class
        {
            if (!_cache.TryGetValue(cacheKey, out List<T>? cachedData) || cachedData == null)
            {
                cachedData = await fallback() ?? new List<T>();
                
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(1));

                _cache.Set(cacheKey, cachedData, cacheEntryOptions);
            }
            return cachedData;
        }
    }
}
