Remove-Item -Path ./*.nupkg -Force

$version = "0.0.1"

nuget pack .\scpp.runtime.win-x64\scpp.runtime.win-x64.nuspec -properties version=$version
nuget pack .\scpp\scpp.nuspec -properties version=$version
