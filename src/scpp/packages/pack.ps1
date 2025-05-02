Remove-Item -Path ./*.nupkg -Force

$version = "0.0.2"

nuget pack .\scpp.runtime.win-x64\scpp.runtime.win-x64.nuspec -properties version=$version
nuget pack .\scpp.runtime.linux-x64\scpp.runtime.linux-x64.nuspec -properties version=$version
nuget pack .\scpp\scpp.nuspec -properties version=$version
