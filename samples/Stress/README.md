# Stress

## How to Run Stress Test

### Debug Orchestrator

You will need to run the Orchestrator project itself either from the cli or setting it as your debug project in Visual Studio.

### Create Certificates

You will also need to generate a `server.pfx` file in the Host directory. This can be accomplished using these steps:

```bash
openssl genrsa 2048 > server.pem

openssl req -x509 -new -key server.pem -out server.cert

openssl pkcs12 -export -in server.cert -inkey server.pem -out server.pfx
```

### Run Redis

The stress project expects there to be a Redis running on localhost:6379. This is readily achievable using Docker by executing:

```bash
docker run -d --rm --name redis-test -p 6379:6379 redis
```

### Run Stress

You should now be able to execute the stress test.