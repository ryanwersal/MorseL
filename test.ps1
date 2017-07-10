Get-ChildItem "." -Recurse -Filter "*Tests.csproj" | 
Foreach-Object {
    $name = $_.FullName
    dotnet test $name
}