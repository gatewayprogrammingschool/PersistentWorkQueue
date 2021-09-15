using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PersistentWorkQueue
{
    public record RequestWrapper<TRequest>(TRequest Request)
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset CompletedOn { get; internal set;}

        public List<(DateTimeOffset timestamp, string result)> Attempts { get; init; } = new();

        public bool IsCanceled { get; internal set; }

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
    }
}
