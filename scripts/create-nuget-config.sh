echo "<?xml version=\"1.0\" encoding=\"utf-8\"?>
<configuration>
    <packageSources>
        <add key=\"NuGet\" value=\"https://api.nuget.org/v3/index.json\" />
        <add key=\"MyGet\" value=\"https://www.myget.org/F/ranger-labs/auth/$1/api/v3/index.json\" />
        <add key=\"StackExchange\" value=\"https://www.myget.org/F/stackoverflow/api/v3/index.json\" />
    </packageSources>
    <activePackageSource>
        <add key=\"All\" value=\"(Aggregate source)\" />
    </activePackageSource>
</configuration>" > NuGet.config
