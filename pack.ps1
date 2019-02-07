param(
    [Parameter(Mandatory=$true)]
    [string]$output,
    [switch]$stable = $false,
    [string]$versionSuffix = $null,
    [switch]$symbols = $false
)

$projects = @(
    "MorseL",
    "MorseL.Common",
    "MorseL.Common.WebSockets",
    "MorseL.Diagnostics",
    "MorseL.Client",
    "MorseL.Sockets",
    "MorseL.Scaleout",
    "MorseL.Scaleout.Redis"
)

$packArguments = New-Object System.Collections.ArrayList
$_ = $packArguments.Add("pack")
$_ = $packArguments.Add("--output")
$_ = $packArguments.Add((Resolve-Path $output))
$_ = $packArguments.Add("--include-source")
$_ = $packArguments.Add("--include-symbols")
$_ = $packArguments.Add("--version-suffix")
$_ = $packArguments.Add($versionSuffix)

if ($stable -Or -Not $versionSuffix) {
    $_ = $packArguments.RemoveAt($packArguments.Count - 1)
    $_ = $packArguments.RemoveAt($packArguments.Count - 1)
}

foreach ($project in $projects) {
    $_ = $packArguments.Add("src\$project\$project.csproj")
    dotnet @packArguments
    $_ = $packArguments.RemoveAt($packArguments.Count - 1)
}