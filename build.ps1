param(
    [switch]$clean = $false
)

if ($clean) {
    dotnet clean .\WebSocketManager.sln
}

dotnet build .\WebSocketManager.sln

Get-ChildItem "." -Recurse -Filter "*Tests.csproj" | 
Foreach-Object {
    $name = $_.FullName
    dotnet test $name
}