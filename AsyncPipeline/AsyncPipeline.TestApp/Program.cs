using AsyncPipeline.Core;

var pipeline = AsyncPipeline<int, int>
    .Start()
    .Step(async x => x * 2)
    .Step(async x => x + 10);

var result = await pipeline.ExecuteAsync(5);
Console.WriteLine(result); // Should print 20