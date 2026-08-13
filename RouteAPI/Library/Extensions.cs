using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Vmvt.RouteAPI;


/// <summary>Plėtiniai</summary>
public static partial class Extensions {
	/// <summary></summary><param name="app"></param><param name="routes"></param>
	public static void UseRouteEndpoints(this WebApplication app, List<RouteDefinition> routes) {
#if DEBUG //Disable Swagger
		app.UseSwagger();
		app.UseSwaggerUI(c => {
			foreach (var i in routes) {
				c.SwaggerEndpoint($"/swagger/{i.Path}/swagger.json", i.Name + " " + i.Version);
				c.InjectStylesheet("/swagger-custom.css");
			}
		});
#endif

		foreach (var i in routes) {
			var eps = 0;
			foreach (var j in i.Routes) {
				foreach (var k in j.Routes) {
					var m = app.Attach(k); eps++;
					if (k.Filter is not null) m.AttachFilter(k.Filter);
#if DEBUG //Disable Swagger
					m.Produces(k.Status, k.Response)
					.WithOpenApi(o => {
						o.Summary = k.Summary; o.Description = k.Description; o.OperationId = k.Code;
						o.Tags = [new() { Name = j.Name }]; o.Deprecated = k.Deprecated ?? false;
						if (k.Params is not null) foreach (var i in k.Params) o.Parameters.Add(i.GetParam()); return o;
					}).WithMetadata(new EndpointGroupNameAttribute($"{i.Path}"));
					if (k.Errors?.Count > 0) { m.Errors([.. k.Errors]); }
#endif
				}
			}
			Console.WriteLine($"Endpoint: {i.Name} {i.Path} / {eps}");
		}

#if DEBUG //Disable Swagger
		app.MapGet("swagger-custom.css", () => ".model-container { margin: 5px 10px !important; }\r\n.model-box { padding: 5px 10px !important; }\r\n.swagger-ui .info { margin:20px 10px !important; }\r\n").ExcludeFromDescription();
#endif
	}

	/// <summary></summary><param name="services"></param><param name="routes"></param><returns></returns>
	public static IServiceCollection AddSwagger(this IServiceCollection services, List<RouteDefinition> routes) {

#if DEBUG //Disable Swagger
		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen(c => {
			foreach (var i in routes)
				c.SwaggerDoc($"{i.Path}", new() { Title = i.Name, Version = i.Version, Description = i.Description });
			foreach (var i in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
				c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, i));
		});
#endif
		return services;
	}

	/// <summary></summary><param name="app"></param><param name="route"></param><returns></returns>
	public static RouteHandlerBuilder Attach(this WebApplication app, Route route) => (route.Method switch {
		Method.Get => app.MapGet(route.Path, route.Handler),
		Method.Post => app.MapPost(route.Path, route.Handler),
		Method.Put => app.MapPut(route.Path, route.Handler),
		Method.Patch => app.MapPatch(route.Path, route.Handler),
		Method.Delete => app.MapDelete(route.Path, route.Handler),
		_ => app.Map(route.Path, route.Handler),
	}).EPFilter(route);


	/// <summary>Pridėti filtro funkciją</summary><param name="builder"></param><param name="filter"></param><returns></returns>
	public static RouteHandlerBuilder AttachFilter(this RouteHandlerBuilder builder, RouteFilter filter) =>
		builder.AddEndpointFilter(async (ctx, next) => await filter(ctx) ?? await next(ctx));

	/// <summary>Registruoti API atsakymo klaidas</summary>
	/// <param name="builder"></param><param name="err"></param><returns></returns>
	public static RouteHandlerBuilder Errors(this RouteHandlerBuilder builder, params int[] err) {
		foreach (var i in err) builder.Produces<ErrorResponse>(i);
		return builder;
	}

	/// <summary>Registruoti API atsakymo formatą</summary>
	/// <typeparam name="T">Formatas</typeparam><param name="builder"></param><returns></returns>
	public static RouteHandlerBuilder Response<T>(this RouteHandlerBuilder builder) => builder.Produces<T>(200);

	/// <summary>Registruoti API atsakymo formatą</summary>
	/// <typeparam name="T">Formatas</typeparam><param name="builder"></param>
	/// <param name="main">Pagrindinis atsakymo statusas</param>
	/// <param name="err">Klaidos kodai</param><returns></returns>
	public static RouteHandlerBuilder Response<T>(this RouteHandlerBuilder builder, int main = 200, params int[] err) => builder.Produces<T>(main).Errors(err);

	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static bool ParamTrue(this HttpContext ctx, string prm) => ctx.Request.Query.TryGetValue(prm, out var flg) && (string.IsNullOrEmpty(flg) || flg == "1" || (bool.TryParse(flg, out var b3) && b3));
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static bool ParamNull(this HttpContext ctx, string prm) => !ctx.Request.Query.TryGetValue(prm, out var flg) || string.IsNullOrEmpty(flg);
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><param name="_default"></param><returns></returns>
	public static int ParamInt(this HttpContext ctx, string prm, int _default = 0) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && int.TryParse(flg, out var num) ? num : _default;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static int? ParamIntN(this HttpContext ctx, string prm) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && int.TryParse(flg, out var num) ? num : null;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><param name="_default"></param><returns></returns>
	public static long ParamLong(this HttpContext ctx, string prm, long _default = 0) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && long.TryParse(flg, out var lng) ? lng : _default;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static long? ParamLongN(this HttpContext ctx, string prm) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && long.TryParse(flg, out var lng) ? lng : null;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><param name="_defult"></param><returns></returns>
	public static DateOnly ParamDate(this HttpContext ctx, string prm, DateOnly _defult = default) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && DateOnly.TryParse(flg, out var dt) ? dt : _defult;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static DateOnly? ParamDateN(this HttpContext ctx, string prm) => ctx.Request.Query.TryGetValue(prm, out var flg) && !string.IsNullOrEmpty(flg) && DateOnly.TryParse(flg, out var dt) ? dt : null;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><param name="_default"></param><returns></returns>
	public static string ParamString(this HttpContext ctx, string prm, string _default = "") => ctx.Request.Query.TryGetValue(prm, out var flg) ? flg.FirstOrDefault() ?? _default : _default;
	/// <summary></summary><param name="ctx"></param><param name="prm"></param><returns></returns>
	public static string? ParamStringN(this HttpContext ctx, string prm) => ctx.Request.Query.TryGetValue(prm, out var flg) ? flg.FirstOrDefault() : null;

	/// <summary></summary><param name="num"></param><param name="max"></param><returns></returns>
	public static int Limit(this int num, int max) => num > max ? max : num;


	/// <summary>Standartinis atsakas</summary>
	public static async Task Ok(this HttpResponse rsp) => await rsp.WriteAsJsonAsync(Ok200);
	/// <summary>Nerasto resurso klaida</summary>
	public static async Task Ok(this HttpResponse rsp, string msg) => await rsp.WriteAsJsonAsync(new DefaultResponse(200, msg));
	private static DefaultResponse Ok200 { get; } = new(200, "Sucess");

	[GeneratedRegex(@"\s+")] private static partial Regex RgxMultiSpace();  /// <summary>Akcento pašalinimas</summary><param name="text"></param><returns></returns>
	public static string RemoveAccents(this string text) {
		var str = text.Normalize(NormalizationForm.FormD);
		var sb = new StringBuilder(capacity: str.Length);
		for (int i = 0; i < str.Length; i++) {
			char c = str[i];
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
		}
		return sb.ToString().Normalize(NormalizationForm.FormC);
	}

	/// <summary>Simbolių pašalinimas</summary><param name="text"></param><param name="exclude">Nešalinami simboliai</param><returns></returns>
	public static string RemoveNonAlphanumeric(this string text, char[]? exclude = null) {
		if (string.IsNullOrEmpty(text)) return text;
		var sb = new StringBuilder();
		foreach (char c in text) {
			//if (exc is not null && exc.Contains(c)) sb.Append(c); else 
			sb.Append(char.IsLetterOrDigit(c) || (exclude?.Contains(c) == true) ? c : ' ');
		}
		return RgxMultiSpace().Replace(sb.ToString(), " ").Trim();
	}

	/// <summary>Žodžių pašalinimas</summary><param name="text"></param><param name="words"></param><returns></returns>
	public static string RemWords(this string text, List<string> words) {
		var sp = text.Split(" "); var ret = new List<string>();
		foreach (var i in sp) if (!words.Contains(i)) ret.Add(i);
		return string.Join(" ", ret);
	}


	/// <summary></summary><param name="lst"></param><param name="val"></param><returns></returns>
	public static string AddN(this List<string> lst, params string?[] val) {
		var ls = val.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(); var ret = "";
		if (ls.Length > 0) { ret = string.Join(" ", ls); lst.Add(ret); }
		return ret;
	}

	/// <summary>Tikrinti ar užklausa yra "json" tipo</summary>
	/// <param name="ctx"></param>
	public static bool IsJson(this HttpContext ctx) => ctx.Request.HasJsonContentType() || ctx.Request.Headers.Accept.Any(h => h?.Contains("application/json") ?? false);


	private static RouteHandlerBuilder EPFilter(this RouteHandlerBuilder rhb, Route rte) {
		if (rte.Before is not null || rte.After is not null) {
			rhb.AddEndpointFilter(async (ivc, next) => {
				if (rte.Before is not null) {
					try { if (!await rte.Before(ivc.HttpContext)) return Results.Empty; }
					catch (Exception ex) {
						Console.WriteLine($"{ex.Message}\n{ex.StackTrace}");
						throw new InvalidOperationException("Fatal configuration error detected in endpoint before filter.");
					}
				}
				var result = await next(ivc);
				if (rte.After is not null) {
					try { await rte.After(ivc.HttpContext); }
					catch (Exception ex) { Console.WriteLine($"ERROR: {ex.Message}\n{ex.StackTrace}"); }
				}
				return result;
			});
		}
		return rhb;
	}

}


/// <summary>Datos formatavimas</summary>
public class CustomDateTimeConverter : JsonConverter<DateTime> {
	/// <summary></summary><param name="reader"></param><param name="typeToConvert"></param><param name="options"></param><returns></returns>
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTime.TryParse(reader.GetString(), out var dt) ? dt : default;
	/// <summary></summary><param name="writer"></param><param name="value"></param><param name="options"></param>
	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
}

/// <summary>Skaičiaus+teksto serializavimas</summary>
public class CustomIntStringTupleConverter : JsonConverter<(int code, string message)> {
	/// <summary></summary><param name="reader"></param><param name="typeToConvert"></param><param name="options"></param><returns></returns>
	public override (int code, string message) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw new NotImplementedException(); }
	/// <summary></summary><param name="writer"></param><param name="value"></param><param name="options"></param>
	public override void Write(Utf8JsonWriter writer, (int code, string message) value, JsonSerializerOptions options) { writer.WriteStartArray(); writer.WriteNumberValue(value.code); writer.WriteStringValue(value.message); writer.WriteEndArray(); }
}