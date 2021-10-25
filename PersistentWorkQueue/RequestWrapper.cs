using System;
using System.Collections.Generic;
#if NET5_0_OR_GREATER
using System.Text.Json;
#else
using System.Collections.Concurrent;
using Newtonsoft.Json;
#endif

namespace PersistentWorkQueue
{
    public record RequestWrapper<TRequest>(TRequest Request)
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset CompletedOn { get; internal set;}

        public List<(DateTimeOffset timestamp, string result)> Attempts { get; init; } = new();

        public bool IsCanceled { get; internal set; }
#if NET5_0_OR_GREATER
        public string Serialize() 
            => JsonSerializer.Serialize(this, typeof(RequestWrapper<TRequest>));

        public string Serialize(JsonSerializerOptions options) 
            => JsonSerializer.Serialize(this, typeof(RequestWrapper<TRequest>), options);

        public void Serialize(Utf8JsonWriter writer) 
            => JsonSerializer.Serialize(writer, this, typeof(RequestWrapper<TRequest>));

        public void Serialize(Utf8JsonWriter writer, JsonSerializerOptions options) 
            => JsonSerializer.Serialize(writer, this, typeof(RequestWrapper<TRequest>), options);

        public static RequestWrapper<TRequest>? Deserialize(string json) 
            => JsonSerializer.Deserialize<RequestWrapper<TRequest>>(json);

        public static RequestWrapper<TRequest>? Deserialize(string json, JsonSerializerOptions options) 
            => JsonSerializer.Deserialize<RequestWrapper<TRequest>>(json, options);

        public static RequestWrapper<TRequest>? Deserialize(ref Utf8JsonReader reader) 
            => JsonSerializer.Deserialize<RequestWrapper<TRequest>>(ref reader);

        public static RequestWrapper<TRequest>? Deserialize(ref Utf8JsonReader reader, JsonSerializerOptions options) 
            => JsonSerializer.Deserialize<RequestWrapper<TRequest>>(ref reader, options);
#else
        public string Serialize()
            => JsonConvert.SerializeObject(Request);

        public string Serialize(JsonSerializerSettings settings)
            => JsonConvert.SerializeObject(Request, settings);

        public static RequestWrapper<TRequest>? Deserialize(string json) 
            => JsonConvert.DeserializeObject<RequestWrapper<TRequest>>(json);

        public static RequestWrapper<TRequest>? Deserialize(string json, JsonSerializerSettings options) 
            => JsonConvert.DeserializeObject<RequestWrapper<TRequest>>(json, options);
#endif
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices {
    internal static class IsExternalInit {}
}

namespace PersistentWorkQueue
{
    public static class UtilityExtensions
    {
        public static void Clear<T>(this ConcurrentQueue<T> source)
        {
            lock (source)
            {
                while (!source.IsEmpty)
                {
                    source.TryDequeue(out _);
                }
            }
        }
    }
}
#endif