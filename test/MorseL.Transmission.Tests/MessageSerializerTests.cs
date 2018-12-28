using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using MorseL.Common.Serialization;
using Newtonsoft.Json;
using Xunit;
using Xunit.Categories;

namespace MorseL.Transmission.Tests
{
    [Category("Transmission")]
    public class MessageSerializerTests
    {
        [Fact]
        public async Task WriteObjectToStreamShouldSerializeObjectAndWriteToStream()
        {
            using (var memoryStream = new MemoryStream())
            {

                var @object = new SomeObject();
                await MessageSerializer.WriteObjectToStreamAsync(memoryStream, @object, leaveOpen: true);

                memoryStream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(memoryStream))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<SomeObject>(await streamReader.ReadToEndAsync());
                    Assert.Equal(@object.Foo, deserializedObject.Foo);
                    Assert.Equal(@object.Bar, deserializedObject.Bar);
                }
            }
        }

        [Fact]
        public async Task WriteObjectToWrappedStreamShouldSerializeObjectAndWriteToStream()
        {
            using (var memoryStream = new MemoryStream())
            {
                var @object = new SomeObject();

                using (var cryptoStream = new CryptoStream(memoryStream, new ToBase64Transform(), CryptoStreamMode.Write, true))
                {
                    await MessageSerializer.WriteObjectToStreamAsync(cryptoStream, @object, leaveOpen: true);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);

                using (var cryptoStream = new CryptoStream(memoryStream, new FromBase64Transform(), CryptoStreamMode.Read, true))
                {
                    using (var streamReader = new StreamReader(cryptoStream))
                    {
                        var deserializedObject = JsonConvert.DeserializeObject<SomeObject>(await streamReader.ReadToEndAsync());
                        Assert.Equal(@object.Foo, deserializedObject.Foo);
                        Assert.Equal(@object.Bar, deserializedObject.Bar);
                    }
                }
            }
        }

        private class SomeObject
        {
            private readonly static Random _random = new Random();

            public int Foo { get; set; } = _random.Next();
            public string Bar { get; set; } = Guid.NewGuid().ToString("N").Substring(0, _random.Next(0, 32));
        }
    }
}
