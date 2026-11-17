using System.Collections.Immutable;
using KeyValue.CLI.Types;

namespace KeyValue.CLI.Service;

public interface IKeyValueStore
{
    IImmutableList<string> GetAll();
    Optional<string> GetOne(string key);
}
