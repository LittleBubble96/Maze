using System;
using UnityEngine;

namespace GameLogic.Maze
{
    /// <summary>Two independently checksummed slots. No old-game cache key is read or overwritten.</summary>
    public sealed class MazeProgressStore : IMazeProgressStore
    {
        private const string Prefix = "PaperFold.Maze.v1.";
        private long _sequence;
        [Serializable] private sealed class Envelope { public long sequence; public string payload, checksum; }
        public MazeProgressData Load()
        {
            Envelope best = null;
            for (int slot = 0; slot < 2; slot++)
            {
                try
                {
                    string raw = Read(Prefix + slot);
                    if (string.IsNullOrEmpty(raw)) continue;
                    var value = JsonUtility.FromJson<Envelope>(raw);
                    if (value == null || value.payload == null || value.checksum != Hash(value.payload)) continue;
                    var data = JsonUtility.FromJson<MazeProgressData>(value.payload);
                    if (data == null || data.schema != 1 || data.levels == null || data.unlocked < 1) continue;
                    if (best == null || value.sequence > best.sequence) best = value;
                }
                catch (Exception error) { Debug.LogWarning("[Maze] 存档槽损坏：" + error.Message); }
            }
            if (best == null) return new MazeProgressData();
            _sequence = best.sequence;
            return JsonUtility.FromJson<MazeProgressData>(best.payload);
        }
        public void Save(MazeProgressData data)
        {
            string payload = JsonUtility.ToJson(data);
            long next = _sequence + 1;
            var envelope = new Envelope { sequence = next, payload = payload, checksum = Hash(payload) };
            Write(Prefix + (next % 2), JsonUtility.ToJson(envelope));
            _sequence = next;
        }
        private static string Hash(string value)
        {
            unchecked { uint hash = 2166136261; foreach (char c in value) hash = (hash ^ c) * 16777619; return hash.ToString("X8"); }
        }
        private static string Read(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WeChatWASM.WXBase.StorageGetStringSync(key, "");
#else
            return PlayerPrefs.GetString(key, "");
#endif
        }
        private static void Write(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WeChatWASM.WXBase.StorageSetStringSync(key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }
    }
}
