using System;
using System.Collections.Generic;
using System.Text;
using MorseL.Shared.Tests;
using Xunit;
using IClientMiddleware = MorseL.Client.Middleware.IMiddleware;
using ClientConnectionContext = MorseL.Client.Middleware.ConnectionContext;
using IHubMiddleware = MorseL.Sockets.Middleware.IMiddleware;
using HubConnectionContext = MorseL.Sockets.Middleware.ConnectionContext;
using System.Security.Cryptography;
using MorseL.Client.Middleware;
using System.Threading.Tasks;
using System.IO;
using MorseL.Sockets.Middleware;
using MorseL.Tests.Encryption;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using MorseL.Client;
using MorseL.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace MorseL.Tests
{
    [Trait("Target", "EndToEndTests")]
    [Trait("Category", "Encryption")]
    public class EndToEndEncryptionTests : IClassFixture<EndToEndEncryptionTests.Context>
    {
        public class Context
        {
            public readonly PortPool PortPool = new PortPool(5100, 5150);
        }

        public class SharedKey
        {
            public event EventHandler<Aes> OnKeyUpdated;

            private Aes _key;
            public Aes Key
            {
                get => _key;
                set
                {
                    _key = value;
                    OnKeyUpdated?.Invoke(this, _key);
                }
            }
        }

        private readonly ITestOutputHelper _testOutputHelper;
        private ILogger _logger;

        private Context _context;

        public EndToEndEncryptionTests(Context context, ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _logger = new TestOutputHelperLogger(_testOutputHelper);
            _context = context;
        }

        [Fact]
        public async void HubMethodInvokeDuringLongMethodResponseTimeDoesNotBlockInvocation()
        {
            var sharedAes = new SharedKey();

            using (var server = new SimpleMorseLServer<EncryptionTestHub>((services, builder) =>
            {
                services.AddSingleton(sharedAes);
                builder.AddMiddleware<EncryptionHubMiddleware>(ServiceLifetime.Singleton);
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);

                var encryptedClientMiddleware = new EncryptionClientMiddleware();
                client.AddMiddleware(encryptedClientMiddleware);

                await client.StartAsync();

                Assert.Equal("derp", await client.Invoke<string>(nameof(EncryptionTestHub.Echo), "derp"));

                // Create the shared key
                var sharedKey = Aes.Create();
                sharedKey.Padding = PaddingMode.PKCS7;
                sharedKey.Mode = CipherMode.CBC;
                sharedKey.GenerateKey();
                sharedKey.GenerateIV();

                var message = new CryptoParameters(sharedKey).ToBytes();

                await client.Invoke(nameof(EncryptionTestHub.EstablishSecureConnection), message);

                encryptedClientMiddleware.SharedKey = sharedKey;

                Assert.Equal("derp", await client.Invoke<string>(nameof(EncryptionTestHub.Echo), "derp"));

                await client.DisposeAsync();
            }
        }

        public class EncryptionTestHub : Hub
        {
            private SharedKey _sharedKey;

            public EncryptionTestHub(SharedKey sharedKey)
            {
                _sharedKey = sharedKey;
            }

            public Task EstablishSecureConnection(byte[] bytes)
            {
                var parameters = CryptoParameters.FromBytes(bytes);

                var key = Aes.Create();
                key.Padding = PaddingMode.PKCS7;
                key.Mode = CipherMode.CBC;
                key.Key = parameters.Key;
                key.IV = parameters.IV;

                _sharedKey.Key = key;

                return Task.CompletedTask;
            }

            public Task<string> Echo(string message)
            {
                return Task.FromResult(message);
            }
        }

        public class EncryptionClientMiddleware : IClientMiddleware, IDisposable
        {
            public Aes SharedKey { get; set; }

            public async Task SendAsync(Stream stream, TransmitDelegate next)
            {
                if (SharedKey == null)
                {
                    await next(stream);
                    return;
                }

                using (var base64Transform = new ToBase64Transform())
                {
                    using (var encodeStream = new CryptoStream(stream, base64Transform, CryptoStreamMode.Write))
                    {
                        using (var encryptor = SharedKey.CreateEncryptor())
                        {
                            using (var encryptStream = new CryptoStream(encodeStream, encryptor, CryptoStreamMode.Write))
                            {
                                await next(encryptStream);
                            }
                        }
                    }
                }
            }

            public async Task RecieveAsync(ClientConnectionContext context, RecieveDelegate next)
            {
                if (SharedKey == null)
                {
                    await next(new ClientConnectionContext(context.ClientWebSocket, context.Stream));
                    return;
                }

                using (var base64Transform = new FromBase64Transform())
                {
                    using (var decodeStream = new CryptoStream(context.Stream, base64Transform, CryptoStreamMode.Read))
                    {
                        using (var decryptor = SharedKey.CreateDecryptor())
                        {
                            using (var decryptStream = new CryptoStream(decodeStream, decryptor, CryptoStreamMode.Read))
                            {
                                await next(new ClientConnectionContext(context.ClientWebSocket, decryptStream));
                            }
                        }
                    }
                }
            }

            public void Dispose()
            {
                SharedKey?.Dispose();
            }
        }

        public class EncryptionHubMiddleware : IHubMiddleware, IDisposable
        {
            private SharedKey _sharedKey;
            private bool _hasRespondedWithSecureSessionResponse = false;

            public EncryptionHubMiddleware(SharedKey sharedKey)
            {
                _sharedKey = sharedKey;
            }

            public async Task SendAsync(HubConnectionContext context, MiddlewareDelegate next)
            {
                if (_sharedKey.Key == null || !_hasRespondedWithSecureSessionResponse)
                {
                    _hasRespondedWithSecureSessionResponse = _sharedKey.Key != null;
                    await next(context);
                    return;
                }

                using (var encryptor = _sharedKey.Key.CreateEncryptor())
                {
                    using (var encryptStream = new CryptoStream(context.Stream, encryptor, CryptoStreamMode.Read))
                    {
                        using (var base64Transform = new ToBase64Transform())
                        {
                            using (var encodeStream = new CryptoStream(encryptStream, base64Transform, CryptoStreamMode.Read))
                            {
                                await next(new HubConnectionContext(context.Connection, encodeStream));
                            }
                        }
                    }
                }
            }

            public async Task ReceiveAsync(HubConnectionContext context, MiddlewareDelegate next)
            {
                if (_sharedKey.Key == null)
                {
                    await next(new HubConnectionContext(context.Connection, context.Stream));
                    return;
                }

                using (var base64Transform = new FromBase64Transform())
                {
                    using (var decodeStream = new CryptoStream(context.Stream, base64Transform, CryptoStreamMode.Read))
                    {
                        using (var decryptor = _sharedKey.Key.CreateDecryptor())
                        {
                            using (var decryptStream = new CryptoStream(decodeStream, decryptor, CryptoStreamMode.Read))
                            {
                                await next(new HubConnectionContext(context.Connection, decryptStream));
                            }
                        }
                    }
                }
            }

            public void Dispose()
            {
                _sharedKey.Key?.Dispose();
            }
        }
    }
}
