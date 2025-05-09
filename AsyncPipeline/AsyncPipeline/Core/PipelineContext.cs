using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPipeline.Core
{
    public class PipelineContext<T>
    {
        public T Value { get; set; }

        public Dictionary<string, object> Metadata { get; } = new();

        public PipelineContext(T value)
        {
            Value = value;
        }

        public void Set(string key, object value) => Metadata[key] = value;

        public TVal? Get<TVal>(string key) =>
            Metadata.TryGetValue(key, out var val) ? (TVal)val : default;
    }
}
