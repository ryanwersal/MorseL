using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WebSocketManager.Common.Serialization
{
    public class WebSocketManagerJsonSerializerSettings : JsonSerializerSettings
    {
        public WebSocketManagerJsonSerializerSettings()
        {
            //TypeNameHandling = TypeNameHandling.All;
            //PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            //DateFormatHandling = DateFormatHandling.IsoDateFormat;
            //DefaultValueHandling = DefaultValueHandling.Ignore;
            //MissingMemberHandling = MissingMemberHandling.Ignore;
            //NullValueHandling = NullValueHandling.Ignore;
            //ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;

            Converters.Add(new GuidJsonConverter());
        }
    }

    internal class GuidJsonConverter : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override bool CanConvert(Type objectType)
        {
            var typeInfo = objectType.GetTypeInfo();
            return typeInfo.IsAssignableFrom(typeof(Guid)) || typeInfo.IsAssignableFrom(typeof(Guid?));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteValue(default(string));
            }
            else if (value is Guid)
            {
                var guid = (Guid)value;
                writer.WriteValue(guid.ToString("N"));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var str = reader.Value as string;
            if (str == null) return default(Guid);

            var success = Guid.TryParse(str, out Guid guid);
            if (!success) return str;
            return guid;
        }
    }
}
