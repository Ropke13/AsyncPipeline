using AsyncPipeline.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPipeline.Tests
{
    public class PipelineTests
    {
        [Fact]
        public async Task Step_Should_Chain_Correctly()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .Step(async x => x * 2)
                .Step(async x => x + 3);

            var result = await pipeline.ExecuteAsync(5);
            Assert.Equal(13, result); // (5 * 2) + 3
        }

        [Fact]
        public async Task StepWithRetry_Should_Retry_And_Succeed()
        {
            int attempt = 0;

            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepWithRetry(async x =>
                {
                    attempt++;
                    if (attempt < 3) throw new Exception("Try again");
                    return x * 10;
                }, retryCount: 3);

            var result = await pipeline.ExecuteAsync(2);
            Assert.Equal(20, result);
            Assert.Equal(3, attempt);
        }

        [Fact]
        public async Task Validate_Should_Throw_When_Invalid()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .Step(async x => x * 2)
                .Validate(x => x > 10, "Too small");

            await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(3));
        }

        [Fact]
        public async Task StepWithTimeout_Should_Throw_If_Too_Slow()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepWithTimeout(
                    async x =>
                    {
                        await Task.Delay(2000);
                        return x;
                    },
                    timeout: TimeSpan.FromMilliseconds(500)
                );

            await Assert.ThrowsAsync<TimeoutException>(() => pipeline.ExecuteAsync(1));
        }

        [Fact]
        public async Task Conditional_Step_Should_Choose_Right_Path()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepIf(
                    x => x % 2 == 0,
                    whenTrue: p => p.Step(async x => x + 100),
                    whenFalse: p => p.Step(async x => x + 1)
                );

            var evenResult = await pipeline.ExecuteAsync(4);
            var oddResult = await pipeline.ExecuteAsync(5);

            Assert.Equal(104, evenResult);
            Assert.Equal(6, oddResult);
        }

        [Fact]
        public async Task AsyncContextPipeline_ShouldRunSteps()
        {
            var pipeline = AsyncContextPipeline<int>
                .Start()
                .Step(async ctx =>
                {
                    ctx.Set("tag", "test");
                })
                .Transform(async ctx =>
                {
                    return ctx.Value + 10;
                })
                .Step(async ctx =>
                {
                    ctx.Value += 5;
                });

            var result = await pipeline.ExecuteAsync(5);

            Assert.Equal(20, result); // 5 + 10 + 5
        }

        [Fact]
        public void PipelineContext_ShouldStoreAndRetrieveMetadata()
        {
            var context = new PipelineContext<string>("hello");
            context.Set("lang", "en");
            context.Set("version", 1);

            var lang = context.Get<string>("lang");
            var version = context.Get<int>("version");
            var missing = context.Get<string>("not-set");

            Assert.Equal("en", lang);
            Assert.Equal(1, version);
            Assert.Null(missing);
        }

        [Fact]
        public async Task StepWithRetry_ShouldThrowAfterMaxAttempts()
        {
            int attempts = 0;

            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepWithRetry<int>(async x =>
                {
                    attempts++;
                    throw new InvalidOperationException("Fail");
                }, retryCount: 2); // Will fail twice

            var ex = await Assert.ThrowsAsync<Exception>(() => pipeline.ExecuteAsync(1));
            Assert.Contains("Step failed after", ex.Message);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public async Task Step_ShouldInvokeErrorHook_OnFailure()
        {
            string? errorStep = null;
            Exception? caught = null;

            var pipeline = AsyncPipeline<int, int>
                .Start()
                .OnStepError((step, ex) =>
                {
                    errorStep = step;
                    caught = ex;
                })
                .Step<int>(async x =>
                {
                    throw new InvalidOperationException("fail");
                }, "FailStep");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(1));

            Assert.Equal("FailStep", errorStep);
            Assert.NotNull(caught);
            Assert.Equal("fail", caught?.Message);
        }

        [Fact]
        public async Task StepWithRetry_ShouldRespectRetryDelay()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int attempts = 0;

            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepWithRetry<int>(async x =>
                {
                    attempts++;
                    throw new Exception("fail");
                }, retryCount: 2, retryDelay: TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAsync<Exception>(() => pipeline.ExecuteAsync(5));

            stopwatch.Stop();
            Assert.Equal(2, attempts);
            Assert.True(stopwatch.ElapsedMilliseconds >= 200); // Ensures delay occurred
        }

        [Fact]
        public async Task Parallel_ShouldRunStepsAndMergeResults()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .Parallel(
                    async x => x * 2,               // 4
                    async x => x + 10,              // 12
                    (a, b) => a + b                 // 4 + 12 = 16
                );

            var result = await pipeline.ExecuteAsync(2);
            Assert.Equal(16, result);
        }

        [Fact]
        public async Task Step_ShouldInvokeStartAndSuccessHooks()
        {
            string? started = null;
            string? succeeded = null;

            var pipeline = AsyncPipeline<int, int>
                .Start()
                .OnStepStart(name => started = name)
                .OnStepSuccess((name, time) => succeeded = name)
                .Step(async x => x + 1, "MyStep");

            var result = await pipeline.ExecuteAsync(5);

            Assert.Equal(6, result);
            Assert.Equal("MyStep", started);
            Assert.Equal("MyStep", succeeded);
        }

        [Fact]
        public async Task StepWithTimeout_ShouldSucceedIfWithinTime()
        {
            var pipeline = AsyncPipeline<int, int>
                .Start()
                .StepWithTimeout(async x =>
                {
                    await Task.Delay(100); // fast
                    return x * 2;
                }, TimeSpan.FromMilliseconds(500), "FastStep");

            var result = await pipeline.ExecuteAsync(5);
            Assert.Equal(10, result);
        }
    }
}
