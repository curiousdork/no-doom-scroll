namespace KeyValue.CLI.Types.Extensions;

/// <summary>
/// Asynchronous functional helpers for <see cref="Result{T, TErr}"/>.
/// </summary>
/// <remarks>
/// Every operation has two overloads:
/// <list type="bullet">
/// <item><description>one on <see cref="Result{T, TErr}"/> that takes an async callback, and</description></item>
/// <item><description>one on <c>Task&lt;Result&lt;T, TErr&gt;&gt;</c>, so you can keep chaining without awaiting every step.</description></item>
/// </list>
/// Success-side callbacks run only on <see cref="Ok{T}"/>; the first <see cref="Err{TErr}"/> short-circuits the
/// chain. For synchronous steps, see <see cref="ResultExtensions"/>.
/// <code>
/// var response = await ValidateAsync(request)            // Task&lt;Result&lt;Request, Error&gt;&gt;
///     .EnsureAsync(r => HasFundsAsync(r), () => Task.FromResult(BudgetErrors.InsufficientFunds))
///     .BindAsync(r => SaveAsync(r))
///     .TapErrorAsync(error => LogAsync(error))
///     .MatchAsync(
///         saved => Task.FromResult(Results.Ok(saved)),
///         error => Task.FromResult(Results.BadRequest(error)));
/// </code>
/// </remarks>
public static class ResultAsyncExtensions
{
    extension<T, TErr>(Result<T, TErr> result)
    {
        /// <summary>
        /// Awaits <paramref name="ok"/> with the value on success, otherwise <paramref name="err"/> with the error,
        /// and returns its result.
        /// </summary>
        /// <exception cref="InvalidOperationException">The result is an uninitialized (<c>default</c>) value.</exception>
        public async Task<TResult> MatchAsync<TResult>(
            Func<T, Task<TResult>> ok,
            Func<TErr, Task<TResult>> err) =>
            result switch
            {
                Ok<T> value => await ok(value.Value).ConfigureAwait(false),
                Err<TErr> error => await err(error.Error).ConfigureAwait(false),
                _ => throw new InvalidOperationException("The result value is not initialized."),
            };

        /// <summary>
        /// Transforms the success value with the async <paramref name="map"/>. An error is passed through and
        /// <paramref name="map"/> is not called.
        /// </summary>
        /// <example><c>var balance = await accountResult.MapAsync(account => GetBalanceAsync(account.Id));</c></example>
        public async Task<Result<TResult, TErr>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            await result.MatchAsync<T, TErr, Result<TResult, TErr>>(
                    async value => new Ok<TResult>(await map(value).ConfigureAwait(false)),
                    static error => Task.FromResult<Result<TResult, TErr>>(new Err<TErr>(error)))
                .ConfigureAwait(false);

        /// <summary>
        /// Transforms the error with the async <paramref name="map"/>, possibly into a different error type.
        /// A success value is passed through unchanged.
        /// </summary>
        /// <example><c>var mapped = await result.MapErrorAsync(error => LocalizeAsync(error));</c></example>
        public async Task<Result<T, TNewErr>> MapErrorAsync<TNewErr>(Func<TErr, Task<TNewErr>> map) =>
            await result.MatchAsync<T, TErr, Result<T, TNewErr>>(
                    static value => Task.FromResult<Result<T, TNewErr>>(new Ok<T>(value)),
                    async error => new Err<TNewErr>(await map(error).ConfigureAwait(false)))
                .ConfigureAwait(false);

        /// <summary>
        /// Chains an async operation that can itself fail (for example a database call). An existing error
        /// short-circuits and <paramref name="bind"/> is not called.
        /// </summary>
        /// <example><c>var saved = await ValidateAsync(request).BindAsync(valid => repository.SaveAsync(valid));</c></example>
        public async Task<Result<TResult, TErr>> BindAsync<TResult>(Func<T, Task<Result<TResult, TErr>>> bind) =>
            await result.Match(bind, static error => Task.FromResult<Result<TResult, TErr>>(new Err<TErr>(error)))
                .ConfigureAwait(false);

        /// <summary>
        /// Awaits <paramref name="action"/> with the success value (for example to publish an event) and returns the
        /// original result unchanged.
        /// </summary>
        /// <example><c>await result.TapAsync(created => events.PublishAsync(new BudgetCreated(created.Id)));</c></example>
        public async Task<Result<T, TErr>> TapAsync(Func<T, Task> action) =>
            await result.Match(
                async value =>
                {
                    await action(value).ConfigureAwait(false);
                    return result;
                },
                _ => Task.FromResult(result)).ConfigureAwait(false);

        /// <summary>
        /// Awaits <paramref name="action"/> with the error (for example to log it) and returns the original result
        /// unchanged.
        /// </summary>
        /// <example><c>await result.TapErrorAsync(error => auditLog.WriteAsync(error.ToString()));</c></example>
        public async Task<Result<T, TErr>> TapErrorAsync(Func<TErr, Task> action) =>
            await result.Match(
                _ => Task.FromResult(result),
                async error =>
                {
                    await action(error).ConfigureAwait(false);
                    return result;
                }).ConfigureAwait(false);

        /// <summary>
        /// Validates the success value with the async <paramref name="predicate"/>. If it returns
        /// <see langword="false"/>, the result becomes an <see cref="Err{TErr}"/> produced by
        /// <paramref name="errorFactory"/>. Existing errors are kept and the predicate is not called.
        /// </summary>
        /// <example><c>var checkedMove = await move.EnsureAsync(m => HasFundsAsync(m), () => Task.FromResult(BudgetErrors.InsufficientFunds));</c></example>
        public async Task<Result<T, TErr>> EnsureAsync(
            Func<T, Task<bool>> predicate,
            Func<Task<TErr>> errorFactory) =>
            await result.MatchAsync<T, TErr, Result<T, TErr>>(
                    async value => await predicate(value).ConfigureAwait(false)
                        ? new Ok<T>(value)
                        : new Err<TErr>(await errorFactory().ConfigureAwait(false)),
                    static error => Task.FromResult<Result<T, TErr>>(new Err<TErr>(error)))
                .ConfigureAwait(false);

        /// <summary>
        /// Returns the success value, or awaits <paramref name="alternative"/> to compute a fallback from the error.
        /// </summary>
        /// <example><c>var settings = await result.ValueOrAsync(_ => LoadDefaultSettingsAsync());</c></example>
        public async Task<T> ValueOrAsync(Func<TErr, Task<T>> alternative) =>
            await result.Match(static value => Task.FromResult(value), alternative).ConfigureAwait(false);

        /// <summary>
        /// Recovers from an error by awaiting <paramref name="alternative"/>, which may succeed or fail with a new
        /// error type. A success value is passed through unchanged.
        /// </summary>
        /// <example><c>var account = await FindInCacheAsync(id).OrElseAsync(_ => FindInDatabaseAsync(id));</c></example>
        public async Task<Result<T, TNewErr>> OrElseAsync<TNewErr>(
            Func<TErr, Task<Result<T, TNewErr>>> alternative) =>
            await result.MatchAsync<T, TErr, Result<T, TNewErr>>(
                static value => Task.FromResult<Result<T, TNewErr>>(new Ok<T>(value)),
                alternative).ConfigureAwait(false);
    }

    extension<T, TErr>(Task<Result<T, TErr>> result)
    {
        /// <summary>Awaits the source result and then matches it asynchronously.</summary>
        public async Task<TResult> MatchAsync<TResult>(
            Func<T, Task<TResult>> ok,
            Func<TErr, Task<TResult>> err) =>
            await (await result.ConfigureAwait(false)).MatchAsync(ok, err).ConfigureAwait(false);

        /// <summary>Awaits the source result and then maps its success value asynchronously.</summary>
        public async Task<Result<TResult, TErr>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            await (await result.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits the source result and then maps its error asynchronously.</summary>
        public async Task<Result<T, TNewErr>> MapErrorAsync<TNewErr>(Func<TErr, Task<TNewErr>> map) =>
            await (await result.ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);

        /// <summary>Awaits the source result and then binds its success value asynchronously.</summary>
        public async Task<Result<TResult, TErr>> BindAsync<TResult>(Func<T, Task<Result<TResult, TErr>>> bind) =>
            await (await result.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits the source result and then runs an async side effect on its success value.</summary>
        public async Task<Result<T, TErr>> TapAsync(Func<T, Task> action) =>
            await (await result.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits the source result and then runs an async side effect on its error.</summary>
        public async Task<Result<T, TErr>> TapErrorAsync(Func<TErr, Task> action) =>
            await (await result.ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits the source result and then validates its success value asynchronously.</summary>
        public async Task<Result<T, TErr>> EnsureAsync(
            Func<T, Task<bool>> predicate,
            Func<Task<TErr>> errorFactory) =>
            await (await result.ConfigureAwait(false)).EnsureAsync(predicate, errorFactory).ConfigureAwait(false);

        /// <summary>Awaits the source result and then returns its value or an asynchronously computed fallback.</summary>
        public async Task<T> ValueOrAsync(Func<TErr, Task<T>> alternative) =>
            await (await result.ConfigureAwait(false)).ValueOrAsync(alternative).ConfigureAwait(false);

        /// <summary>Awaits the source result and then recovers from its error asynchronously.</summary>
        public async Task<Result<T, TNewErr>> OrElseAsync<TNewErr>(
            Func<TErr, Task<Result<T, TNewErr>>> alternative) =>
            await (await result.ConfigureAwait(false)).OrElseAsync(alternative).ConfigureAwait(false);
    }
}
