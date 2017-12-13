# MorseL
MorseL is a light, .NET Core, replacement for SignalR. It's in...marginally active development.

This project exists because certain features (authentication) weren't baked into .NET Core SignalR. Additionally, the .NET Core libraries were _too_ bleading edge to be used in an ongoing project. The intention is for MorseL to be swappable with SignalR when it stabilizes.

## Building
./builds.sh

## Testing
./test.sh

## Deploying
./pack.sh
./deploy-nuget.sh c:/myget/repo

## Using MorseL
Look at [/samples](./tree/master/samples)