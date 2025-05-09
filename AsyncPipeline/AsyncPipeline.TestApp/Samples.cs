using AsyncPipeline.Core;

//1. Basic Chaining
var result1 = await AsyncPipeline<int, int>
    .Start()
    .Step(async x => x * 2)
    .Step(async x => x + 3)
    .ExecuteAsync(5); // → (5 * 2) + 3 = 13

//2. Retry Logic (per step)
int attempts = 0;

var result2 = await AsyncPipeline<int, int>
    .Start()
    .StepWithRetry(async x =>
    {
        attempts++;
        if (attempts < 3)
            throw new Exception("Fail");
        return x + 1;
    }, retryCount: 5)
    .ExecuteAsync(10); // → 11 after 3 attempts

//3. Timeout Step
var result3 = await AsyncPipeline<int, int>
    .Start()
    .StepWithTimeout(async x =>
    {
        await Task.Delay(500);
        return x * 2;
    }, TimeSpan.FromSeconds(1))
    .ExecuteAsync(5); // → 10

//4. Validation Step
var pipeline4 = AsyncPipeline<int, int>
    .Start()
    .Step(async x => x * 2)
    .Validate(x => x >= 10, "Value must be ≥ 10")
    .Step(async x => x + 1);

var result = await pipeline4.ExecuteAsync(6); // 13

// Throws if input = 3 (3 * 2 = 6 < 10)

//5. Conditional Branching
var result5 = await AsyncPipeline<int, int>
    .Start()
    .StepIf(
        condition: x => x % 2 == 0,
        whenTrue: p => p.Step(async x => x + 100),
        whenFalse: p => p.Step(async x => x + 1)
    )
    .ExecuteAsync(4); // → 104

//6. Parallel Steps + Merge
var result6 = await AsyncPipeline<int, int>
    .Start()
    .Parallel(
        async x => x * 2,
        async x => x + 10,
        (a, b) => a + b
    )
    .ExecuteAsync(5); // (5 * 2) + (5 + 10) = 10 + 15 = 25

//7. Step Hooks
var result7 = AsyncPipeline<int, int>
    .Start()
    .OnStepStart(name => Console.WriteLine($"➡ {name} started"))
    .OnStepSuccess((name, time) => Console.WriteLine($"✅ {name} succeeded in {time.TotalMilliseconds}ms"))
    .OnStepError((name, ex) => Console.WriteLine($"❌ {name} failed: {ex.Message}"))
    .Step(async x => x * 2, "Double")
    .Step(async x => x + 1, "Add");

await result7.ExecuteAsync(5);


//8. Pipeline Context Usage
var result8 = AsyncContextPipeline<int>
    .Start()
    .Step(async ctx => ctx.Set("Tag", "TestRun"))
    .Transform(async ctx => ctx.Value * 2)
    .Step(async ctx =>
    {
        Console.WriteLine($"Metadata: {ctx.Get<string>("Tag")}");
        ctx.Value += 1;
    });

var result8Final = await result8.ExecuteAsync(5); // Output: 11
