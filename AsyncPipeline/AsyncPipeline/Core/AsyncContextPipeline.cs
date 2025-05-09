using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPipeline.Core
{
    public class AsyncContextPipeline<T>
    {
        private readonly List<Func<PipelineContext<T>, Task<PipelineContext<T>>>> _steps = new();

        public static AsyncContextPipeline<T> Start()
        {
            return new AsyncContextPipeline<T>();
        }

        public AsyncContextPipeline<T> Step(Func<PipelineContext<T>, Task> stepFunc)
        {
            _steps.Add(async ctx =>
            {
                await stepFunc(ctx);
                return ctx;
            });
            return this;
        }

        public AsyncContextPipeline<T> Transform(Func<PipelineContext<T>, Task<T>> transform)
        {
            _steps.Add(async ctx =>
            {
                ctx.Value = await transform(ctx);
                return ctx;
            });
            return this;
        }

        public async Task<T> ExecuteAsync(T input)
        {
            var context = new PipelineContext<T>(input);

            foreach (var step in _steps)
            {
                context = await step(context);
            }

            return context.Value;
        }
    }
}
