namespace KeyValue.CLI.Types.Extensions;

/// <summary>
/// Synchronous functional helpers for <see cref="Optional{T}"/>.
/// </summary>
/// <remarks>
/// An <see cref="Optional{T}"/> is either <see cref="Some{T}"/> (holds a value) or <see cref="None"/> (no value).
/// Operations run their callbacks only on <see cref="Some{T}"/> and pass <see cref="None"/> through untouched,
/// so you can chain them without checking for missing values at every step.
/// <code>
/// var name = FindAccount(id)              // Optional&lt;Account&gt;
///     .Filter(account => account.IsActive)
///     .Map(account => account.Name)
///     .ValueOr("Unknown account");
/// </code>
/// </remarks>
public static class OptionalExtensions
{
    extension<T>(Optional<T> optional)
    {
        /// <summary>
        /// Runs <paramref name="some"/> with the value when present, otherwise runs <paramref name="none"/>,
        /// and returns what that function returns. This is the basic way to get a value out of an optional.
        /// </summary>
        /// <example><c>var label = optional.Match(value => $"Value: {value}", () => "Empty");</c></example>
        /// <exception cref="InvalidOperationException">The optional is an uninitialized (<c>default</c>) value.</exception>
        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) =>
            optional switch
            {
                Some<T> value => some(value.Value),
                None => none(),
                _ => throw new InvalidOperationException("The optional value is not initialized."),
            };

        /// <summary>
        /// Runs <paramref name="some"/> with the value when present, otherwise runs <paramref name="none"/>.
        /// Use it for side effects when you don't need a return value.
        /// </summary>
        /// <example><c>optional.Match(value => Console.WriteLine(value), () => Console.WriteLine("Empty"));</c></example>
        /// <exception cref="InvalidOperationException">The optional is an uninitialized (<c>default</c>) value.</exception>
        public void Match(Action<T> some, Action none)
        {
            switch (optional)
            {
                case Some<T> value:
                    some(value.Value);
                    break;
                case None:
                    none();
                    break;
                default:
                    throw new InvalidOperationException("The optional value is not initialized.");
            }
        }

        /// <summary>
        /// Transforms the value with <paramref name="map"/> when present. <see cref="None"/> stays <see cref="None"/>.
        /// </summary>
        /// <example><c>Optional&lt;int&gt; length = name.Map(value => value.Length);</c></example>
        public Optional<TResult> Map<TResult>(Func<T, TResult> map) =>
            optional.Match<T, Optional<TResult>>(
                value => new Some<TResult>(map(value)),
                () => new None());

        /// <summary>
        /// Chains an operation that itself returns an optional (also known as <c>FlatMap</c>).
        /// Use it instead of <c>Map</c> when the function can produce <see cref="None"/>,
        /// so you don't end up with <c>Optional&lt;Optional&lt;T&gt;&gt;</c>.
        /// </summary>
        /// <example><c>Optional&lt;Bucket&gt; bucket = FindAccount(id).Bind(account => FindBucket(account.DefaultBucketId));</c></example>
        public Optional<TResult> Bind<TResult>(Func<T, Optional<TResult>> bind) =>
            optional.Match(bind, static () => new None());

        /// <summary>
        /// Keeps the value only if it satisfies <paramref name="predicate"/>; otherwise returns <see cref="None"/>.
        /// </summary>
        /// <example><c>Optional&lt;int&gt; positive = amount.Filter(value => value > 0);</c></example>
        public Optional<T> Filter(Func<T, bool> predicate) =>
            optional.Match<T, Optional<T>>(
                value => predicate(value) ? optional : new None(),
                static () => new None());

        /// <summary>
        /// Runs <paramref name="action"/> with the value when present (for example to log it) and returns the
        /// original optional unchanged, so the chain can continue.
        /// </summary>
        /// <example><c>optional.Tap(value => logger.LogInformation("Found {Value}", value)).Map(...);</c></example>
        public Optional<T> Tap(Action<T> action)
        {
            optional.Match(action, static () => { });
            return optional;
        }

        /// <summary>
        /// Runs <paramref name="action"/> only when the optional is <see cref="None"/> and returns the original
        /// optional unchanged.
        /// </summary>
        /// <example><c>optional.TapNone(() => logger.LogWarning("Account not found"));</c></example>
        public Optional<T> TapNone(Action action)
        {
            optional.Match(static _ => { }, action);
            return optional;
        }

        /// <summary>Returns <see langword="true"/> when the optional holds a value.</summary>
        public bool IsSome() => optional.Match(static _ => true, static () => false);

        /// <summary>Returns <see langword="true"/> when the optional holds no value.</summary>
        public bool IsNone() => optional.Match(static _ => false, static () => true);

        /// <summary>
        /// Returns the value when present, otherwise <paramref name="defaultValue"/> (which is <c>default</c>/<c>null</c>
        /// when omitted). Prefer <c>ValueOr</c> when you want a non-nullable result.
        /// </summary>
        /// <example><c>string? name = optional.GetValueOrDefault();</c></example>
        public T? GetValueOrDefault(T? defaultValue = default) =>
            optional.Match(static value => value, () => defaultValue);

        /// <summary>Returns the value when present, otherwise <paramref name="alternative"/>.</summary>
        /// <example><c>decimal balance = optional.ValueOr(0m);</c></example>
        public T ValueOr(T alternative) =>
            optional.Match(static value => value, () => alternative);

        /// <summary>
        /// Returns the value when present, otherwise calls <paramref name="alternative"/> to produce one.
        /// The factory is only invoked when needed, so use this overload for expensive fallbacks.
        /// </summary>
        /// <example><c>var settings = optional.ValueOr(() => LoadDefaultSettings());</c></example>
        public T ValueOr(Func<T> alternative) =>
            optional.Match(static value => value, alternative);

        /// <summary>
        /// Returns this optional when it holds a value, otherwise returns <paramref name="alternative"/>.
        /// </summary>
        /// <example><c>var bucket = userBucket.Or(defaultBucket);</c></example>
        public Optional<T> Or(Optional<T> alternative) =>
            optional.Match(_ => optional, () => alternative);

        /// <summary>
        /// Returns this optional when it holds a value, otherwise calls <paramref name="alternative"/> to try another
        /// source. The factory is only invoked when needed.
        /// </summary>
        /// <example><c>var account = FindInCache(id).OrElse(() => FindInDatabase(id));</c></example>
        public Optional<T> OrElse(Func<Optional<T>> alternative) =>
            optional.Match(_ => optional, alternative);

        /// <summary>
        /// Converts to a <see cref="Result{T, TErr}"/>: <see cref="Some{T}"/> becomes <see cref="Ok{T}"/> and
        /// <see cref="None"/> becomes <see cref="Err{TErr}"/> holding <paramref name="error"/>.
        /// </summary>
        /// <example><c>Result&lt;Account, Error&gt; result = FindAccount(id).ToResult(AccountErrors.NotFound);</c></example>
        public Result<T, TErr> ToResult<TErr>(TErr error) =>
            optional.Match<T, Result<T, TErr>>(
                static value => new Ok<T>(value),
                () => new Err<TErr>(error));

        /// <summary>
        /// Pairs two optionals into a tuple. Returns <see cref="Some{T}"/> only when both hold a value,
        /// otherwise <see cref="None"/>.
        /// </summary>
        /// <example><c>var transfer = source.Zip(target).Map(pair => (From: pair.First, To: pair.Second));</c></example>
        public Optional<(T First, TOther Second)> Zip<TOther>(Optional<TOther> other) =>
            optional.Bind(first => other.Map(second => (first, second)));

        /// <summary>
        /// LINQ support (same as <c>Map</c>). Enables <c>from x in optional select ...</c>.
        /// </summary>
        public Optional<TResult> Select<TResult>(Func<T, TResult> selector) =>
            optional.Map(selector);

        /// <summary>LINQ support (same as <c>Bind</c>).</summary>
        public Optional<TResult> SelectMany<TResult>(Func<T, Optional<TResult>> selector) =>
            optional.Bind(selector);

        /// <summary>
        /// LINQ support for multiple <c>from</c> clauses. The query yields <see cref="None"/> as soon as any source is
        /// <see cref="None"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// Optional&lt;decimal&gt; total = from a in first
        ///                            from b in second
        ///                            select a + b;
        /// </code>
        /// </example>
        public Optional<TResult> SelectMany<TIntermediate, TResult>(
            Func<T, Optional<TIntermediate>> selector,
            Func<T, TIntermediate, TResult> resultSelector) =>
            optional.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));
    }

    extension<T>(Optional<Optional<T>> optional)
    {
        /// <summary>Unwraps a nested <c>Optional&lt;Optional&lt;T&gt;&gt;</c> into a single <see cref="Optional{T}"/>.</summary>
        public Optional<T> Flatten() => optional.Bind(static value => value);
    }

    extension<T>(T? value)
        where T : class
    {
        /// <summary>
        /// Wraps a nullable reference: <see langword="null"/> becomes <see cref="None"/>, anything else
        /// becomes <see cref="Some{T}"/>. Handy for EF Core queries such as <c>FirstOrDefaultAsync</c>.
        /// </summary>
        /// <example><c>Optional&lt;Account&gt; account = dbContext.Accounts.Find(id).ToOptional();</c></example>
        public Optional<T> ToOptional() =>
            value is null ? new None() : new Some<T>(value);
    }

    extension<T>(T? value)
        where T : struct
    {
        /// <summary>
        /// Wraps a nullable value type: <see langword="null"/> becomes <see cref="None"/>, anything else
        /// becomes <see cref="Some{T}"/>.
        /// </summary>
        /// <example><c>Optional&lt;int&gt; count = ((int?)null).ToOptional();</c></example>
        public Optional<T> ToOptional() =>
            value.HasValue ? new Some<T>(value.Value) : new None();
    }
}
