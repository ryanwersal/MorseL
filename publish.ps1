param(
    [switch]$publish = $false,
    [switch]$stable = $false,
    [string]$versionSuffix = "1"
)

$versionSuffix = "--version-suffix $versionSuffix"

$repo = "c:\myget\"
if ($publish) {
    $repo = "z:\Engineering\RoboMobile\OurGet\"
}

if ($stable) {
    $versionSuffix = ""
}

dotnet pack --include-source --include-symbols $versionSuffix src\WebSocketManager\WebSocketManager.csproj
dotnet pack --include-source --include-symbols $versionSuffix src\WebSocketManager.Common\WebSocketManager.Common.csproj
dotnet pack --include-source --include-symbols $versionSuffix src\WebSocketManager.Client\WebSocketManager.Client.csproj
dotnet pack --include-source --include-symbols $versionSuffix src\WebSocketManager.Sockets\WebSocketManager.Sockets.csproj
dotnet pack --include-source --include-symbols $versionSuffix src\AsyncWebSocketClient\AsyncWebSocketClient.csproj

Get-ChildItem "." -Recurse -Filter "*.nupkg" | 
Foreach-Object {
    $name = $_.FullName
    nuget add $name -Source $repo -NonInteractive
    Remove-Item $name
}