using System.Collections.Immutable;
using KeyValue.CLI.Types;
using KeyValue.CLI.Types.Extensions;

namespace KeyValue.CLI.Service;

public sealed class KeyValueStore : IKeyValueStore
{
    private Dictionary<string, string> _dictionary;

    public KeyValueStore() => _dictionary = new Dictionary<string, string>();

    public Optional<string> GetOne(string key)
    {
        if (!_dictionary.TryGetValue(key, out string? value)) return new None();

        return value.ToOptional();
    }

    public IImmutableList<string> GetAll()
    {
        return [];
    }
}