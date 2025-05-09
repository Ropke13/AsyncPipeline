using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPipeline.Core
{
    public class AsyncPipeline<TIn, TOut>
    {
        private readonly List<Func<object, Task<object>>> _steps;

        private Action<string>? _onStepStart;
        private Action<string, TimeSpan>? _onStepSuccess;
        private Action<string, Exception>? _onStepError;

        private AsyncPipeline(List<Func<object, Task<object>>> steps)
        {
            _steps = steps;
        }

        private AsyncPipeline()
        {
            _steps = new List<Func<object, Task<object>>>();
        }

        public static AsyncPipeline<TIn, TIn> Start()
        {
            return new AsyncPipeline<TIn, TIn>();
        }

        public AsyncPipeline<TIn, TNext> Step<TNext>(Func<TOut, Task<TNext>> stepFunc, string? name = null)
        {
            var stepName = name ?? $"Step{_steps.Count + 1}";

            _steps.Add(async input =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _onStepStart?.Invoke(stepName);

                try
                {
                    var result = await stepFunc((TOut)input);
                    _onStepSuccess?.Invoke(stepName, sw.Elapsed);
                    return (object)result!;
                }
                catch (Exception ex)
                {
                    _onStepError?.Invoke(stepName, ex);
                    throw;
                }
            });

            return new AsyncPipeline<TIn, TNext>(_steps)
            {
                _onStepStart = this._onStepStart,
                _onStepSuccess = this._onStepSuccess,
                _onStepError = this._onStepError
            };
        }

        public AsyncPipeline<TIn, TNext> StepWithRetry<TNext>(
            Func<TOut, Task<TNext>> stepFunc,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            _steps.Add(async input =>
            {
                int attempt = 0;
                Exception? lastException = null;

                while (attempt < retryCount)
                {
                    try
                    {
                        var result = await stepFunc((TOut)input);
                        return (object)result!;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        attempt++;

                        if (attempt < retryCount && retryDelay.HasValue)
                        {
                            await Task.Delay(retryDelay.Value);
                        }
                    }
                }

                throw new Exception($"Step failed after {retryCount} retries.", lastException);
            });

            return new AsyncPipeline<TIn, TNext>(_steps);
        }

        public async Task<TOut> ExecuteAsync(TIn input)
        {
            object current = input;

            foreach (var step in _steps)
            {
                current = await step(current);
            }

            return (TOut)current;
        }

        public AsyncPipeline<TIn, TNext> StepIf<TNext>(
            Func<TOut, bool> condition,
            Func<AsyncPipeline<TOut, TOut>, AsyncPipeline<TOut, TNext>> whenTrue,
            Func<AsyncPipeline<TOut, TOut>, AsyncPipeline<TOut, TNext>> whenFalse)
        {
            _steps.Add(async input =>
            {
                var typedInput = (TOut)input;

                var branchPipeline = condition(typedInput)
                    ? whenTrue(AsyncPipeline<TOut, TOut>.Start())
                    : whenFalse(AsyncPipeline<TOut, TOut>.Start());

                var result = await branchPipeline.ExecuteAsync(typedInput);
                return (object)result!;
            });

            return new AsyncPipeline<TIn, TNext>(_steps);
        }

        public AsyncPipeline<TIn, TNext> Parallel<T1, T2, TNext>(
            Func<TOut, Task<T1>> step1,
            Func<TOut, Task<T2>> step2,
            Func<T1, T2, TNext> merge)
        {
            _steps.Add(async input =>
            {
                var typedInput = (TOut)input;

                var task1 = step1(typedInput);
                var task2 = step2(typedInput);

                await Task.WhenAll(task1, task2);

                return (object)merge(task1.Result, task2.Result)!;
            });

            return new AsyncPipeline<TIn, TNext>(_steps);
        }

        public AsyncPipeline<TIn, TOut> Validate(
            Func<TOut, bool> predicate,
            string errorMessage = "Validation failed.")
        {
            _steps.Add(async input =>
            {
                var typed = (TOut)input;

                if (!predicate(typed))
                {
                    throw new InvalidOperationException(errorMessage);
                }

                return input;
            });

            return new AsyncPipeline<TIn, TOut>(_steps);
        }

        public AsyncPipeline<TIn, TOut> OnStepStart(Action<string> action)
        {
            _onStepStart = action;
            return this;
        }

        public AsyncPipeline<TIn, TOut> OnStepSuccess(Action<string, TimeSpan> action)
        {
            _onStepSuccess = action;
            return this;
        }

        public AsyncPipeline<TIn, TOut> OnStepError(Action<string, Exception> action)
        {
            _onStepError = action;
            return this;
        }

        public AsyncPipeline<TIn, TNext> StepWithTimeout<TNext>(
            Func<TOut, Task<TNext>> stepFunc,
            TimeSpan timeout,
            string? name = null)
        {
            var stepName = name ?? $"Step{_steps.Count + 1}";

            _steps.Add(async input =>
            {
                var typedInput = (TOut)input;

                var stepTask = stepFunc(typedInput);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(stepTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"Step '{stepName}' exceeded timeout of {timeout.TotalSeconds} seconds.");
                }

                return (object)(await stepTask)!;
            });

            return new AsyncPipeline<TIn, TNext>(_steps);
        }
    }
}
