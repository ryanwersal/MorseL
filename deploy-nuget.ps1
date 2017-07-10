param(
    [Parameter(Mandatory=$true)][string]$artifacts,
    [Parameter(Mandatory=$true)][string]$repo
)

$filter = "*.nupkg"
if ($symbols) {
    $filter = "*symbols.nupkg"
}

Get-ChildItem (Resolve-Path $artifacts) -Recurse -Filter $filter | 
Foreach-Object {
    $name = $_.FullName
    nuget add $name -Source $repo -NonInteractive
    Remove-Item $name
}