using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenSleigh.Core.Utils
{
    /// <summary>
    /// can't use System.Text.Json, polymorfic support is not mature: https://github.com/dotnet/runtime/issues/45189
    /// </summary>
    public class JsonSerializer : ITransportSerializer, IPersistenceSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public ValueTask<byte[]> SerializeAsync<T>(T state, CancellationToken cancellationToken = default)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(state, Settings);
            var bytes = Encoding.UTF8.GetBytes(json);            
            return ValueTask.FromResult(bytes);
        }

        public ValueTask<T> DeserializeAsync<T>(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            var json = Encoding.UTF8.GetString(data.Span);
            var serialized = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json, Settings);
            return ValueTask.FromResult(serialized);
        }

        public object Deserialize(ReadOnlyMemory<byte> data, Type type)
        {
            var json = System.Text.Encoding.UTF8.GetString(data.Span);
            return Newtonsoft.Json.JsonConvert.DeserializeObject(json, type, Settings);
        }            
    }
}