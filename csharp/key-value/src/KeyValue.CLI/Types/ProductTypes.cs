using System.Linq.Expressions;

namespace KeyValue.CLI.Types;

public sealed record Some<T>(T Value);

public sealed record None;

public abstract record Ok;

public sealed record Ok<T>(T Value) : Ok where T : notnull;

public sealed record Err<TErr>(TErr Error);


public union Optional<T>(Some<T>, None);

public union Result<T, TErr>(Ok<T>, Err<TErr>);


