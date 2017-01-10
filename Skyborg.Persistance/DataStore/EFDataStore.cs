using Google.Apis.Util.Store;
using Newtonsoft.Json;
using Skyborg.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Skyborg.Persistance.DataStore
{
    public class EFDataStore : IDataStore
    {
        public async Task ClearAsync()
        {
            using (var context = new SkyborgDataModel())
            {
                var objectContext = ((IObjectContextAdapter)context).ObjectContext;
                await objectContext.ExecuteStoreCommandAsync("TRUNCATE TABLE [Items]");
            }
        }

        public async Task DeleteAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key MUST have a value");
            }

            using (var context = new SkyborgDataModel())
            {
                var generatedKey = GenerateStoredKey(key, typeof(T));
                var item = context.GoogleOAuthItem.FirstOrDefault(x => x.Key == generatedKey);
                if (item != null)
                {
                    context.GoogleOAuthItem.Remove(item);
                    await context.SaveChangesAsync();
                }
            }
        }

        public Task<T> GetAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key MUST have a value");
            }

            using (var context = new SkyborgDataModel())
            {
                var generatedKey = GenerateStoredKey(key, typeof(T));
                var item = context.GoogleOAuthItem.FirstOrDefault(x => x.Key == generatedKey);
                T value = item == null ? default(T) : JsonConvert.DeserializeObject<T>(item.Value);
                return Task.FromResult<T>(value);
            }
        }

        public async Task StoreAsync<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key MUST have a value");
            }

            using (var context = new SkyborgDataModel())
            {
                var generatedKey = GenerateStoredKey(key, typeof(T));
                string json = JsonConvert.SerializeObject(value);

                var item = await context.GoogleOAuthItem.SingleOrDefaultAsync(x => x.Key == generatedKey);

                if (item == null)
                {
                    context.GoogleOAuthItem.Add(new GoogleOAuthItem { Key = generatedKey, Value = json });
                }
                else
                {
                    item.Value = json;
                }

                await context.SaveChangesAsync();
            }
        }

        private static string GenerateStoredKey(string key, Type t)
        {
            return string.Format("{0}-{1}", t.FullName, key);
        }
    }
}