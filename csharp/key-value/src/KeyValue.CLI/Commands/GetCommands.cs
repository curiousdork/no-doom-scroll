
using System.Collections.Immutable;
using KeyValue.CLI.Service;
using KeyValue.CLI.Types;

namespace KeyValue.CLI.Commands;

public sealed class GetCommands(IKeyValueStore kvs)
{
    public Optional<string> ListOne(string key) 
    {
        return kvs.GetOne(key);
    }
    
    public IImmutableList<string> ListAll()
    {
        return kvs.GetAll();
    }
}