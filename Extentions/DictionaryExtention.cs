using System;
using System.Collections.Generic;

namespace OpenGSServer
{
    public static class DictionaryExtension
    {
        public static TV GetOrDefault<TK, TV>(this Dictionary<TK, TV> dic, TK key, TV defaultValue = default) =>
            dic.TryGetValue(key, out var result) ? result : defaultValue;

        public static TV GetOrAdd<TK, TV>(this Dictionary<TK, TV> dic, TK key, Func<TV> factory)
        {
            if (dic == null)
            {
                throw new ArgumentNullException(nameof(dic));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (dic.TryGetValue(key, out var value))
            {
                return value;
            }

            value = factory();
            dic[key] = value;
            return value;
        }

        public static void AddOrUpdate<TK, TV>(this Dictionary<TK, TV> dic, TK key, TV value)
        {
            if (dic == null)
            {
                throw new ArgumentNullException(nameof(dic));
            }

            dic[key] = value;
        }

        public static bool TryRemove<TK, TV>(this Dictionary<TK, TV> dic, TK key, out TV value)
        {
            value = default;
            if (dic == null)
            {
                return false;
            }

            if (!dic.TryGetValue(key, out value))
            {
                return false;
            }

            return dic.Remove(key);
        }
    }
}
