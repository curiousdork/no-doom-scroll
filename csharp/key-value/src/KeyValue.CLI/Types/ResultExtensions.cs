namespace KeyValue.CLI.Types.Extensions;

/// <summary>
/// Synchronous functional helpers for <see cref="Result{T, TErr}"/>.
/// </summary>
/// <remarks>
/// A <see cref="Result{T, TErr}"/> is either <see cref="Ok{T}"/> (success value) or <see cref="Err{TErr}"/>
/// (failure). Success-side operations (<c>Map</c>, <c>Bind</c>, <c>Ensure</c>, <c>Tap</c>) only run on
/// <see cref="Ok{T}"/>; the first <see cref="Err{TErr}"/> short-circuits the rest of the chain
/// ("railway-oriented programming").
/// <code>
/// IResult response = ParseAmount(input)                  // Result&lt;decimal, Error&gt;
///     .Ensure(amount => amount > 0, () => BudgetErrors.InvalidAmount)
///     .Map(amount => new MoneyMove(amount))
///     .Match(move => Results.Ok(move), error => Results.BadRequest(error));
/// </code>
/// </remarks>
public static class ResultExtensions
{
    extension<T, TErr>(Result<T, TErr> result)
    {
        /// <summary>
        /// Runs <paramref name="ok"/> with the value on success, otherwise <paramref name="err"/> with the error,
        /// and returns what that function returns. This is the usual way to end a chain.
        /// </summary>
        /// <example><c>var message = result.Match(value => $"Saved {value}", error => error.Message);</c></example>
        /// <exception cref="InvalidOperationException">The result is an uninitialized (<c>default</c>) value.</exception>
        public TResult Match<TResult>(Func<T, TResult> ok, Func<TErr, TResult> err) =>
            result switch
            {
                Ok<T> value => ok(value.Value),
                Err<TErr> error => err(error.Error),
                _ => throw new InvalidOperationException("The result value is not initialized."),
            };

        /// <summary>
        /// Runs <paramref name="ok"/> with the value on success, otherwise <paramref name="err"/> with the error.
        /// Use it for side effects when you don't need a return value.
        /// </summary>
        /// <exception cref="InvalidOperationException">The result is an uninitialized (<c>default</c>) value.</exception>
        public void Match(Action<T> ok, Action<TErr> err)
        {
            switch (result)
            {
                case Ok<T> value:
                    ok(value.Value);
                    break;
                case Err<TErr> error:
                    err(error.Error);
                    break;
                default:
                    throw new InvalidOperationException("The result value is not initialized.");
            }
        }

        /// <summary>
        /// Transforms the success value with <paramref name="map"/>. An error is passed through unchanged.
        /// </summary>
        /// <example><c>Result&lt;string, Error&gt; name = accountResult.Map(account => account.Name);</c></example>
        public Result<TResult, TErr> Map<TResult>(Func<T, TResult> map) =>
            result.Match<T, TErr, Result<TResult, TErr>>(
                value => new Ok<TResult>(map(value)),
                static error => new Err<TErr>(error));

        /// <summary>
        /// Transforms the error with <paramref name="map"/> (for example to translate a low-level error into a
        /// domain error). A success value is passed through unchanged.
        /// </summary>
        /// <example><c>Result&lt;Account, Error&gt; mapped = dbResult.MapError(ex => new Error("db", ex.Message));</c></example>
        public Result<T, TNewErr> MapError<TNewErr>(Func<TErr, TNewErr> map) =>
            result.Match<T, TErr, Result<T, TNewErr>>(
                static value => new Ok<T>(value),
                error => new Err<TNewErr>(map(error)));

        /// <summary>
        /// Chains an operation that can itself fail. Runs <paramref name="bind"/> on success and returns its
        /// result; an existing error short-circuits and <paramref name="bind"/> is not called.
        /// </summary>
        /// <example><c>var moved = Validate(request).Bind(valid => Withdraw(valid)).Bind(withdrawn => Deposit(withdrawn));</c></example>
        public Result<TResult, TErr> Bind<TResult>(Func<T, Result<TResult, TErr>> bind) =>
            result.Match(bind, static error => new Err<TErr>(error));

        /// <summary>
        /// Runs <paramref name="action"/> with the success value (for example to log it) and returns the original
        /// result unchanged.
        /// </summary>
        /// <example><c>result.Tap(value => logger.LogInformation("Created {Id}", value.Id));</c></example>
        public Result<T, TErr> Tap(Action<T> action)
        {
            result.Match(action, static _ => { });
            return result;
        }

        /// <summary>
        /// Runs <paramref name="action"/> with the error (for example to log it) and returns the original result
        /// unchanged.
        /// </summary>
        /// <example><c>result.TapError(error => logger.LogWarning("Failed: {Error}", error));</c></example>
        public Result<T, TErr> TapError(Action<TErr> action)
        {
            result.Match(static _ => { }, action);
            return result;
        }

        /// <summary>
        /// Validates the success value. If <paramref name="predicate"/> returns <see langword="false"/>, the result
        /// becomes an <see cref="Err{TErr}"/> created by <paramref name="errorFactory"/>. Existing errors are kept.
        /// </summary>
        /// <example><c>var checkedAmount = amountResult.Ensure(amount => amount > 0, () => BudgetErrors.InvalidAmount);</c></example>
        public Result<T, TErr> Ensure(Func<T, bool> predicate, Func<TErr> errorFactory) =>
            result.Match<T, TErr, Result<T, TErr>>(
                value => predicate(value) ? new Ok<T>(value) : new Err<TErr>(errorFactory()),
                static error => new Err<TErr>(error));

        /// <summary>Returns the success value, or <paramref name="alternative"/> when the result is an error.</summary>
        /// <example><c>decimal balance = balanceResult.ValueOr(0m);</c></example>
        public T ValueOr(T alternative) =>
            result.Match(static value => value, _ => alternative);

        /// <summary>
        /// Returns the success value, or computes a fallback from the error with <paramref name="alternative"/>.
        /// </summary>
        /// <example><c>string text = result.ValueOr(error => $"Error: {error}");</c></example>
        public T ValueOr(Func<TErr, T> alternative) =>
            result.Match(static value => value, alternative);

        /// <summary>
        /// Recovers from an error by calling <paramref name="alternative"/>, which may succeed or fail with a new
        /// error type. A success value is passed through unchanged.
        /// </summary>
        /// <example><c>var account = FindInCache(id).OrElse(_ => FindInDatabase(id));</c></example>
        public Result<T, TNewErr> OrElse<TNewErr>(Func<TErr, Result<T, TNewErr>> alternative) =>
            result.Match<T, TErr, Result<T, TNewErr>>(
                static value => new Ok<T>(value),
                alternative);

        /// <summary>Returns <see langword="true"/> when the result is a success.</summary>
        public bool IsOk() => result is Ok<T>;

        /// <summary>Returns <see langword="true"/> when the result is an error.</summary>
        public bool IsErr() => result is Err<TErr>;

        /// <summary>
        /// Converts to an <see cref="Optional{T}"/>: <see cref="Ok{T}"/> becomes <see cref="Some{T}"/> and the error
        /// is discarded as <see cref="None"/>.
        /// </summary>
        public Optional<T> ToOptional() =>
            result.Match<T, TErr, Optional<T>>(static value => new Some<T>(value), static _ => new None());

        /// <summary>
        /// LINQ support (same as <c>Map</c>). Enables <c>from x in result select ...</c>.
        /// </summary>
        public Result<TResult, TErr> Select<TResult>(Func<T, TResult> selector) =>
            result.Map(selector);

        /// <summary>LINQ support (same as <c>Bind</c>).</summary>
        public Result<TResult, TErr> SelectMany<TResult>(Func<T, Result<TResult, TErr>> selector) =>
            result.Bind(selector);

        /// <summary>
        /// LINQ support for multiple <c>from</c> clauses. The query stops at the first <see cref="Err{TErr}"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// Result&lt;MoneyMove, Error&gt; move = from source in FindBucket(fromId)
        ///                                 from target in FindBucket(toId)
        ///                                 select new MoneyMove(source, target);
        /// </code>
        /// </example>
        public Result<TResult, TErr> SelectMany<TIntermediate, TResult>(
            Func<T, Result<TIntermediate, TErr>> selector,
            Func<T, TIntermediate, TResult> resultSelector) =>
            result.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));
    }

    extension<T, TErr>(Result<Result<T, TErr>, TErr> result)
    {
        /// <summary>Unwraps a nested <c>Result&lt;Result&lt;T, TErr&gt;, TErr&gt;</c> into a single result.</summary>
        public Result<T, TErr> Flatten() => result.Bind(static value => value);
    }

    extension<T, TErr>(IEnumerable<Result<T, TErr>> results)
    {
        /// <summary>
        /// Turns a sequence of results into one result: <see cref="Ok{T}"/> with all values (in order) when every item
        /// succeeds, otherwise the first <see cref="Err{TErr}"/> encountered. Enumeration stops at the first error.
        /// </summary>
        /// <example><c>Result&lt;IReadOnlyList&lt;Bucket&gt;, Error&gt; buckets = ids.Select(FindBucket).Combine();</c></example>
        /// <exception cref="InvalidOperationException">An item is an uninitialized (<c>default</c>) result.</exception>
        public Result<IReadOnlyList<T>, TErr> Combine()
        {
            var values = new List<T>();

            foreach (var result in results)
            {
                switch (result)
                {
                    case Ok<T> value:
                        values.Add(value.Value);
                        break;
                    case Err<TErr> error:
                        return new Err<TErr>(error.Error);
                    default:
                        throw new InvalidOperationException("The result value is not initialized.");
                }
            }

            return new Ok<IReadOnlyList<T>>(values);
        }
    }
}
