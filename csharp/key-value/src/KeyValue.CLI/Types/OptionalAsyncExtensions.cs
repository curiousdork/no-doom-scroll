namespace KeyValue.CLI.Types.Extensions;

/// <summary>
/// Asynchronous functional helpers for <see cref="Optional{T}"/>.
/// </summary>
/// <remarks>
/// Every operation has two overloads:
/// <list type="bullet">
/// <item><description>one on <see cref="Optional{T}"/> that takes an async callback, and</description></item>
/// <item><description>one on <c>Task&lt;Optional&lt;T&gt;&gt;</c>, so you can keep chaining without awaiting every step.</description></item>
/// </list>
/// Callbacks run only when a value is present; <see cref="None"/> flows through without invoking them.
/// For synchronous steps in the middle of a chain, see <see cref="OptionalExtensions"/>.
/// <code>
/// var name = await FindAccountAsync(id)                 // Task&lt;Optional&lt;Account&gt;&gt;
///     .FilterAsync(account => IsActiveAsync(account))
///     .MapAsync(account => LoadDisplayNameAsync(account))
///     .ValueOrAsync(() => Task.FromResult("Unknown account"));
/// </code>
/// </remarks>
public static class OptionalAsyncExtensions
{
    extension<T>(Optional<T> optional)
    {
        /// <summary>
        /// Awaits <paramref name="some"/> with the value when present, otherwise awaits <paramref name="none"/>,
        /// and returns its result.
        /// </summary>
        /// <example><c>var dto = await optional.MatchAsync(account => ToDtoAsync(account), () => Task.FromResult(EmptyDto));</c></example>
        public async Task<TResult> MatchAsync<TResult>(
            Func<T, Task<TResult>> some,
            Func<Task<TResult>> none) =>
            await optional.Match(some, none).ConfigureAwait(false);

        /// <summary>
        /// Transforms the value with the async <paramref name="map"/> when present. <see cref="None"/> stays
        /// <see cref="None"/> and <paramref name="map"/> is not called.
        /// </summary>
        /// <example><c>Optional&lt;decimal&gt; balance = await account.MapAsync(a => GetBalanceAsync(a.Id));</c></example>
        public async Task<Optional<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            await optional.Match<T, Task<Optional<TResult>>>(
                async value => new Some<TResult>(await map(value).ConfigureAwait(false)),
                static () => Task.FromResult<Optional<TResult>>(new None())).ConfigureAwait(false);

        /// <summary>
        /// Chains an async operation that itself returns an optional, such as another lookup.
        /// Short-circuits to <see cref="None"/> when the source has no value.
        /// </summary>
        /// <example><c>var bucket = await FindAccountAsync(id).BindAsync(a => FindBucketAsync(a.DefaultBucketId));</c></example>
        public async Task<Optional<TResult>> BindAsync<TResult>(Func<T, Task<Optional<TResult>>> bind) =>
            await optional.Match(
                bind,
                static () => Task.FromResult<Optional<TResult>>(new None())).ConfigureAwait(false);

        /// <summary>
        /// Keeps the value only if the async <paramref name="predicate"/> returns <see langword="true"/>;
        /// otherwise returns <see cref="None"/>.
        /// </summary>
        /// <example><c>var editable = await budget.FilterAsync(b => CanEditAsync(userId, b));</c></example>
        public async Task<Optional<T>> FilterAsync(Func<T, Task<bool>> predicate) =>
            await optional.Match(
                async value => await predicate(value).ConfigureAwait(false) ? optional : new None(),
                static () => Task.FromResult<Optional<T>>(new None())).ConfigureAwait(false);

        /// <summary>
        /// Awaits <paramref name="action"/> with the value when present (for example to publish an event) and
        /// returns the original optional unchanged.
        /// </summary>
        /// <example><c>await account.TapAsync(a => auditLog.WriteAsync($"Viewed {a.Id}"));</c></example>
        public async Task<Optional<T>> TapAsync(Func<T, Task> action) =>
            await optional.Match(
                async value =>
                {
                    await action(value).ConfigureAwait(false);
                    return optional;
                },
                () => Task.FromResult(optional)).ConfigureAwait(false);

        /// <summary>
        /// Awaits <paramref name="action"/> only when the optional is <see cref="None"/> and returns the original
        /// optional unchanged.
        /// </summary>
        /// <example><c>await account.TapNoneAsync(() => metrics.RecordMissAsync("account"));</c></example>
        public async Task<Optional<T>> TapNoneAsync(Func<Task> action) =>
            await optional.Match<T, Task<Optional<T>>>(
                _ => Task.FromResult(optional),
                async () =>
                {
                    await action().ConfigureAwait(false);
                    return optional;
                }).ConfigureAwait(false);

        /// <summary>
        /// Returns the value when present, otherwise awaits <paramref name="alternative"/> to produce one.
        /// </summary>
        /// <example><c>var settings = await optional.ValueOrAsync(() => LoadDefaultSettingsAsync());</c></example>
        public async Task<T> ValueOrAsync(Func<Task<T>> alternative) =>
            await optional.Match(static value => Task.FromResult(value), alternative).ConfigureAwait(false);

        /// <summary>
        /// Returns this optional when it holds a value, otherwise awaits <paramref name="alternative"/> to try
        /// another source.
        /// </summary>
        /// <example><c>var account = await FindInCacheAsync(id).OrElseAsync(() => FindInDatabaseAsync(id));</c></example>
        public async Task<Optional<T>> OrElseAsync(Func<Task<Optional<T>>> alternative) =>
            await optional.Match(
                _ => Task.FromResult(optional),
                alternative).ConfigureAwait(false);

        /// <summary>
        /// Converts to a <see cref="Result{T, TErr}"/>: <see cref="Some{T}"/> becomes <see cref="Ok{T}"/>, and
        /// <see cref="None"/> becomes <see cref="Err{TErr}"/> with the error produced by <paramref name="errorFactory"/>.
        /// The factory is only awaited when the value is missing.
        /// </summary>
        /// <example><c>var result = await FindAccountAsync(id).ToResultAsync(() => Task.FromResult(AccountErrors.NotFound));</c></example>
        public async Task<Result<T, TErr>> ToResultAsync<TErr>(Func<Task<TErr>> errorFactory) =>
            await optional.Match<T, Task<Result<T, TErr>>>(
                static value => Task.FromResult<Result<T, TErr>>(new Ok<T>(value)),
                async () => new Err<TErr>(await errorFactory().ConfigureAwait(false))).ConfigureAwait(false);
    }

    extension<T>(Task<Optional<T>> optional)
    {
        /// <summary>Awaits the source optional and then matches it asynchronously.</summary>
        public async Task<TResult> MatchAsync<TResult>(
            Func<T, Task<TResult>> some,
            Func<Task<TResult>> none) =>
            await (await optional.ConfigureAwait(false)).MatchAsync(some, none).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then maps its value asynchronously.</summary>
        public async Task<Optional<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            await (await optional.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then binds its value asynchronously.</summary>
        public async Task<Optional<TResult>> BindAsync<TResult>(Func<T, Task<Optional<TResult>>> bind) =>
            await (await optional.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then filters its value asynchronously.</summary>
        public async Task<Optional<T>> FilterAsync(Func<T, Task<bool>> predicate) =>
            await (await optional.ConfigureAwait(false)).FilterAsync(predicate).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then runs an async side effect on its value.</summary>
        public async Task<Optional<T>> TapAsync(Func<T, Task> action) =>
            await (await optional.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then runs an async side effect when it is <see cref="None"/>.</summary>
        public async Task<Optional<T>> TapNoneAsync(Func<Task> action) =>
            await (await optional.ConfigureAwait(false)).TapNoneAsync(action).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then returns its value or an asynchronously produced fallback.</summary>
        public async Task<T> ValueOrAsync(Func<Task<T>> alternative) =>
            await (await optional.ConfigureAwait(false)).ValueOrAsync(alternative).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then falls back to another async source when it is <see cref="None"/>.</summary>
        public async Task<Optional<T>> OrElseAsync(Func<Task<Optional<T>>> alternative) =>
            await (await optional.ConfigureAwait(false)).OrElseAsync(alternative).ConfigureAwait(false);

        /// <summary>Awaits the source optional and then converts it to a <see cref="Result{T, TErr}"/>.</summary>
        public async Task<Result<T, TErr>> ToResultAsync<TErr>(Func<Task<TErr>> errorFactory) =>
            await (await optional.ConfigureAwait(false)).ToResultAsync(errorFactory).ConfigureAwait(false);
    }
}
