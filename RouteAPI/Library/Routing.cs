using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

#if DEBUG
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
#endif

namespace Vmvt.RouteAPI;

/// <summary></summary>
/// <param name="name"></param>
public class RouteDefinition(string name) {
	/// <summary></summary>
	public string Name { get; set; } = name;
	/// <summary></summary>
	public string? Description { get; set; }
	/// <summary></summary>
	public string? Version { get; set; } = "v1";
	/// <summary></summary>
	public string? Tag { get; set; }
	/// <summary></summary>
	[JsonIgnore] public string? Path => (string.IsNullOrEmpty(Tag) ? "" : Tag + "_") + Version;
	/// <summary></summary>
	public List<RouteGroup> Routes { get; set; } = [];
	/// <summary></summary>
	public RouteDefinition Add(RouteGroup group) { Routes.Add(group); return this; }
}

/// <summary></summary>
/// <param name="name"></param>
public class RouteGroup(string name) {
	/// <summary></summary>
	public string Name { get; set; } = name;
	/// <summary></summary>
	public List<Route> Routes { get; } = [];
	/// <summary></summary>
	public RouteGroup Map(Route route) { Routes.Add(route); return this; }
}

/// <summary></summary>
public enum Method {
	/// <summary>HTTP GET</summary>
	Get, 
	/// <summary>HTTP POST</summary>
	Post,
	/// <summary>HTTP PUT</summary>
	Put,
	/// <summary>HTTP PATCH</summary>
	Patch, 
	/// <summary>HTTP DELETE</summary>
	Delete 
}

/// <summary></summary>
/// <param name="path"></param>
/// <param name="hnd"></param>
/// <param name="method"></param>
public class Route(string path, Delegate hnd, Method method = Method.Get) {
	/// <summary>Maršruto kodas (operationId)</summary>
	public string? Code { get; set; }
	/// <summary></summary>
	public string? Description { get; set; }
	/// <summary></summary>
	public string? Summary { get; set; }
	/// <summary></summary>
	public Method Method { get; set; } = method;
	/// <summary></summary>
	public string Path { get; set; } = path;
	/// <summary></summary>
	public int Status { get; set; } = 200;
	/// <summary></summary>
	[JsonIgnore] public Type? Response { get; set; }
	/// <summary></summary>
	public List<int>? Errors { get; set; }
	/// <summary></summary>
	public bool? Deprecated { get; set; }
	/// <summary>Parametrai</summary>
	[JsonIgnore] public List<RouteParam>? Params { get; set; }
	/// <summary>Pagrindinė funkcija</summary>
	[JsonIgnore] public Delegate Handler { get; set; } = hnd; 
	/// <summary>Funkcija paleidžiama prieš pagrindinę užklausą</summary>
	[JsonIgnore] public Func<HttpContext, Task<bool>>? Before { get; set; }
	/// <summary>Funkcija paleidžiama po pagrindinės užklausos</summary>
	[JsonIgnore] public Func<HttpContext, Task<bool>>? After { get; set; }
	/// <summary>Maršruto filtras</summary>
	[JsonIgnore] public RouteFilter? Filter { get; set; }
}


/// <summary></summary>
public enum RouteParamType {
	/// <summary>null type</summary>
	Null = 1,
	/// <summary>boolean type</summary>
	Boolean = 2,
	/// <summary>integer type</summary>
	Integer = 4,
	/// <summary>number type</summary>
	Number = 8,
	/// <summary>string type</summary>
	String = 16,
	/// <summary>object type</summary>
	Object = 32,
	/// <summary>array type</summary>
	Array = 64,
}


/// <summary>Parametro vieta</summary>
public enum RouteParamLoc {
	/// <summary>Adreso parametrai</summary>
	Query,
	/// <summary>Užklausos antraštė</summary>
	Header,
	/// <summary>Adreso dalyje</summary>
	Path,
	/// <summary>"Cookie" reikšmė</summary>
	Cookie
}

/// <summary></summary>
/// <param name="name"></param>
public class RouteParam(string name) {
	/// <summary></summary>
	public string Name { get; set; } = name;
	/// <summary></summary>
	public string? Description { get; set; }
	/// <summary></summary>
	public RouteParamType Type { get; set; } = RouteParamType.Boolean;
	/// <summary></summary>
	public string? Format { get; set; }
	/// <summary></summary>
	public bool Required { get; set; }
	/// <summary></summary>
	public bool Deprecated { get; set; }
	/// <summary></summary>
	public string? Default { get; set; }
	/// <summary></summary>
	public RouteParamLoc Location { get; set; } = RouteParamLoc.Query;
#if DEBUG
	/// <summary></summary>
	/// <returns></returns>
	public OpenApiParameter GetParam() => new() {
		Name = Name, In = (ParameterLocation)Location, Description = Description, Required = Required, Deprecated= Deprecated, Schema = new OpenApiSchema() {
			Type = Type.ToString().ToLower(), Format = string.IsNullOrEmpty(Format) ? null : Format, Default = string.IsNullOrEmpty(Default) ? null : new OpenApiString(Default)
		}
	};
#endif
}

/// <summary>Užklausos filtras</summary><param name="context"></param><returns></returns>
public delegate ValueTask<object?> RouteFilter(EndpointFilterInvocationContext context);
