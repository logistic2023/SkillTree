using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver,
    IEnumerable<KeyValuePair<TKey, TValue>>
{
    [SerializeField] private List<TKey> _keys = new();
    [SerializeField] private List<TValue> _values = new();

    private Dictionary<TKey, TValue> _dict = new();

    public TValue this[TKey key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    public int Count => _dict.Count;
    public bool ContainsKey(TKey key) => _dict.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value);
    public void Remove(TKey key) => _dict.Remove(key);
    public void Clear() => _dict.Clear();

    // IEnumerable<T> — makes foreach work correctly
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();

    // Called before Unity serializes this object
    public void OnBeforeSerialize()
    {
        _keys.Clear();
        _values.Clear();
        foreach (var kvp in _dict)
        {
            _keys.Add(kvp.Key);
            _values.Add(kvp.Value);
        }
    }

    // Called after Unity deserializes this object
    public void OnAfterDeserialize()
    {
        _dict = new Dictionary<TKey, TValue>();
        for (int i = 0; i < _keys.Count; i++)
            _dict[_keys[i]] = _values[i];
    }
}