using System;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Unity's <see cref="JsonUtility"/> cannot deserialise a top-level JSON array
    /// (e.g. <c>[ {...}, {...} ]</c>). This wraps the array in an object so it can.
    /// Used by the editor importer to read the Godot data files.
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<T>();
            string wrapped = "{\"items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return wrapper?.items ?? Array.Empty<T>();
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
