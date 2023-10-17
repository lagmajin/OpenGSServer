using System;
using System.Collections.Generic;


namespace OpenGSServer
{
    public static class DictionaryExtension
    {
        public static TV GetOrDefault<TK, TV>(this Dictionary<TK, TV> dic, TK key, TV defaultValue = default) => dic.TryGetValue(key, out var result) ? result : defaultValue;

        //public static TValue GetValueOrDefalut<TKey, TValue>(this Dictionary<TKey, TValue> source, TKey key, Func<TKey, TValue> func);
    }
}
