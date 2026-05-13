# RouteAPI

A lightweight C# library for Minimal API routing.


### 🛠️ Instrukcija (*.csproj)

 Test/prod išskyrimas draudžiant gamybinėje aplinkoje naudoti swagger

```xml
<ItemGroup Condition="'$(Configuration)'=='Debug'">
	<PackageReference Include="Vmvt.RouteAPI.Swagger" Version="0.1.6" />
</ItemGroup>
<ItemGroup Condition="'$(Configuration)'=='Release'">
	<PackageReference Include="Vmvt.RouteAPI" Version="0.1.6" />
</ItemGroup>
```

Papildomai galima prisidėti "Local" versiją naudojant VMVT-LT/NuGet.NET kodą

```xml
<ItemGroup Condition="'$(Configuration)'=='Local'">
	<ProjectReference Include="..\..\..\NuGet.NET\RouteAPI\Library\RouteAPI.csproj" />
</ItemGroup>
```
