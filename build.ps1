param(
    [switch]$clean = $false
)

if ($clean) {
    dotnet clean .\WebSocketManager.sln
    Get-ChildItem ./ -include obj -recurse | foreach ($_) {Remove-Item -Recurse -Force $_.fullname}
    Get-ChildItem ./ -include bin -recurse | foreach ($_) {Remove-Item -Recurse -Force $_.fullname}
}

Get-ChildItem "." -Recurse -Filter "*.csproj" | 
Foreach-Object {
    dotnet restore $_.FullName
    dotnet build $_.FullName
}

Get-ChildItem "." -Recurse -Filter "*Tests.csproj" | 
Foreach-Object {
    $name = $_.FullName
    dotnet test $name
}