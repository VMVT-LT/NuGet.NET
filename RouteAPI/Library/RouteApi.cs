using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Vmvt.RouteAPI;



/// <summary>RouteAPI iniciavimo modelis</summary>
public class RouteApi {

	/// <summary>API maršrutai</summary>
	public List<RouteDefinition> Endpoints { get; } = [];

	/// <summary></summary>
	/// <param name="route">Maršrutai</param>
	public RouteApi(params RouteDefinition[] route) { Endpoints.AddRange(route); }


	private const string RouteNamespace = "Vmvt.RouteAPI.Modules";
	private const string RouteMethod = "Route";

	/// <summary>Add available routes</summary>
	public RouteApi(string nameSpace = RouteNamespace) {
		foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"{nameSpace}.*.dll"))
			try { Assembly.LoadFrom(file); }
			catch (Exception ex) {
				Console.WriteLine($"Error loading assembly {Path.GetFileName(file)}: {ex.Message}");
			}

		var routes = new List<RouteDefinition>();
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			if (assembly.FullName is not null && assembly.FullName.StartsWith(nameSpace))
				foreach (var i in assembly.ExportedTypes)
					if (i.Namespace!.StartsWith(nameSpace) && i.IsClass && !i.IsInterface && !i.IsAbstract) {
						var mtd = i.GetMethod(RouteMethod, BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
						if (mtd is not null && mtd.ReturnType == typeof(RouteDefinition) && mtd.Invoke(null, null) is RouteDefinition definition)
							routes.Add(definition);
					}
		var lst = routes.GroupBy(rd => rd.Path).Select(group => {
			var grp = group.First();
			var lst = group.SelectMany(rd => rd.Routes).ToList();
			return new RouteDefinition(grp.Name) { Description = grp.Description, Version = grp.Version, Tag = grp.Tag, Routes = lst };
		}).ToList().OrderBy(x => x.Tag);
		Endpoints.AddRange(lst);
	}

	/// <summary>Leisti naudotis API iš kitų puslapių</summary>
	public List<string>? AllowCors { get; set; }


	/// <summary>Build minimal API app</summary>
	/// <param name="build">Perform builder configuration</param>
	/// <returns>WebApplication</returns>
	public WebApplication Build(Action<WebApplicationBuilder>? build = null) {
		// Load nugets
		AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
			var pth = Path.Combine(AppContext.BaseDirectory, "lib", new AssemblyName(args.Name).Name + ".dll");
			return File.Exists(pth) ? Assembly.LoadFrom(pth) : null;
		};

		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseKestrel(option => option.AddServerHeader = false);

		var knownNet = builder.Configuration.GetSection("NetForwarders").Get<List<string>>() ?? [];
#if DEBUG
		Console.WriteLine("NetForwarders: " + string.Join(", ", knownNet));
#endif
		builder.Services.Configure<ForwardedHeadersOptions>(options => {
			options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
			foreach (var i in knownNet) options.KnownNetworks.Add(IPNetwork.Parse(i));
		});

		builder.Services.AddSwagger(Endpoints);

		builder.Services.ConfigureHttpJsonOptions(a => {
			var so = a.SerializerOptions; 
			so.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			so.WriteIndented = false;
			so.Converters.Add(new CustomDateTimeConverter());
			so.Converters.Add(new CustomIntStringTupleConverter());
			so.Converters.Add(new JsonStringEnumConverter());
		});

		if (build is not null) build(builder);

		if (AllowCors?.Count > 0) {
			builder.Services.AddCors(options => {
				options.AddPolicy("AllowFrom", policy => {
					policy.SetIsOriginAllowed(origin => {
						if (string.IsNullOrEmpty(origin)) return false;
						foreach (var i in AllowCors)
							if (origin.StartsWith(i, StringComparison.OrdinalIgnoreCase)) return true;
						return false;
					}).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
				});
			});
		}

		var app = builder.Build(); 
		app.UseForwardedHeaders();
		app.UseExceptionHandler(exh => exh.Run(HandleError));
		app.UseRouteEndpoints(Endpoints);

		app.Use(async (ctx, next) => {
			await next(ctx);
			var code = ctx.Response.StatusCode;

			if (StatusHandler is not null && code is < 200 or >= 300) {
				var err = ctx.GetError();
				if (ctx.IsJson()) {
					if (!ctx.Response.HasStarted) {
						if (err is not null) await ctx.Response.WriteAsJsonAsync(err);
						else err = code switch {
							400 => await ctx.Response.E400(),
							401 => await ctx.Response.E401(),
							403 => await ctx.Response.E403(),
							404 => await ctx.Response.E404(),
							_ => await ctx.Response.Error(code, "Nenumatyta klaida"),
						};
					}
				}
				else if (code is 301 or 302 or 306 or 307) return;
				await StatusHandler(ctx, err);
			}
		});

		if (AllowCors?.Count > 0) app.UseCors("AllowFrom");

		return app;
	}

	/// <summary>Statuso aprodorojimas</summary>
	public Func<HttpContext, ErrorResponse?, Task>? StatusHandler { get; set; }

	/// <summary>Klaidų aprodorojimas</summary>
	public Func<HttpContext, IExceptionHandlerFeature, Task> ErrorHandler { get; set; } = async (ctx, ex) => await ctx.Response.WriteAsync("Error...");

	private async Task HandleError(HttpContext ctx) {
		var ex = ctx.Features.Get<IExceptionHandlerFeature>();
		if (ex is not null && ex.Error is not null) await ErrorHandler(ctx, ex);
		else if (StatusHandler is not null) await StatusHandler(ctx, ctx.GetError());

	}
}

