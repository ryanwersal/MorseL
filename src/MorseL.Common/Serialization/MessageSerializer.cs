﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MorseL.Common.Serialization
{
    internal static class MessageSerializer
    {
        private static readonly JsonSerializer _serializer = new JsonSerializer();
        private static readonly MorseLJsonSerializerSettings _settings = new MorseLJsonSerializerSettings();

        public static T Deserialize<T>(string jsonString)
        {
            return JsonConvert.DeserializeObject<T>(jsonString, _settings);
        }

        public static async Task<T> DeserializeAsync<T>(Stream stream, Encoding encoding)
        {
            using (var internalStreamReader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(internalStreamReader))
                {
                    return (await JObject.ReadFromAsync(jsonReader)).ToObject<T>();
                }
            }
        }

        public static InvocationDescriptor DeserializeInvocationDescriptor(
            string jsonString, ConcurrentDictionary<string, InvocationHandler> handlers)
        {
            using (var stringReader = new StringReader(jsonString))
            {
                using (var textReader = new JsonTextReader(stringReader))
                {
                    var json = _serializer.Deserialize<JObject>(textReader);
                    if (json == null) return null;

                    var invocationDescriptor = new InvocationDescriptor
                    {
                        Id = json.Value<string>("Id"),
                        MethodName = json.Value<string>("MethodName")
                    };

                    if (!handlers.ContainsKey(invocationDescriptor.MethodName))
                    {
                        return null;
                    }

                    var argTypes = handlers[invocationDescriptor.MethodName].ParameterTypes;
                    invocationDescriptor.Arguments = new object[argTypes.Length];

                    var args = json.Value<JArray>("Arguments");
                    for (var i = 0; i < argTypes.Length; ++i)
                    {
                        var argType = argTypes[i];
                        invocationDescriptor.Arguments[i] = args[i].ToObject(argType, _serializer);
                    }

                    return invocationDescriptor;
                }
            }
        }

        public static InvocationDescriptor DeserializeInvocationDescriptor(
            string jsonString, MethodInfo[] handlerMethods)
        {
            using (var stringReader = new StringReader(jsonString))
            {
                using (var textReader = new JsonTextReader(stringReader)) {
                    var json = _serializer.Deserialize<JObject>(textReader);
                    if (json == null) return null;

                    var invocationDescriptor = new InvocationDescriptor
                    {
                        Id = json.Value<string>("Id"),
                        MethodName = json.Value<string>("MethodName")
                    };

                    var method = handlerMethods.FirstOrDefault(m => m.Name == invocationDescriptor.MethodName);
                    if (method == null) return null;

                    var argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    invocationDescriptor.Arguments = new object[argTypes.Length];

                    var args = json.Value<JArray>("Arguments");
                    for (var i = 0; i < argTypes.Length; ++i)
                    {
                        var argType = argTypes[i];
                        invocationDescriptor.Arguments[i] = args[i].ToObject(argType, _serializer);
                    }

                    return invocationDescriptor;
                }
            }
        }

        public static InvocationResultDescriptor DeserializeInvocationResultDescriptor(
            string jsonString, Dictionary<string, InvocationRequest> handlers)
        {
            using (var stringReader = new StringReader(jsonString))
            {
                using (var textReader = new JsonTextReader(stringReader)) {
                    var json = _serializer.Deserialize<JObject>(textReader);
                    if (json == null) return null;


                    var id = json.Value<string>("Id");
                    var returnType = handlers[id].ResultType;
                    if (!handlers.ContainsKey(id)) throw new InvalidInvocationResultException(jsonString, id);
                    var invocationResultDescriptor = new InvocationResultDescriptor
                    {
                        Id = id,
                        Result = returnType == null ? null : json["Result"].ToObject(returnType, _serializer),
                        Error = json.Value<string>("Error")
                    };
                    return invocationResultDescriptor;
                }
            }
        }

        public static string SerializeObject<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        public static async Task WriteObjectToStreamAsync<T>(Stream stream, T obj, bool leaveOpen = false)
        {
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 8192, leaveOpen))
            {
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    // TODO :'(
                    // https://github.com/JamesNK/Newtonsoft.Json/issues/1795
                    await Task.Run(() => _serializer.Serialize(jsonWriter, obj));
                }
            }
        }
    }
}
