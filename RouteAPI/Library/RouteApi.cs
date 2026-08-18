using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
			try { Assembly.LoadFrom(file); } catch (Exception ex) {
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
		Endpoints.AddRange(routes.OrderBy(x => x.Tag));
	}


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

		var app = builder.Build(); 
		app.UseForwardedHeaders();
		app.UseExceptionHandler(exh => exh.Run(HandleError));
		app.UseStatusCodePages(async scc => {
			var ctx = scc.HttpContext;
		//	switch (ctx.Response.StatusCode) {
		//		case 401: ctx.Response
		//	}
			if (StatusHandler is not null) await StatusHandler(ctx);
			else if (!ctx.Response.HasStarted) await ctx.Response.Error();
		});
		app.UseRouteEndpoints(Endpoints);

		return app;
	}

	/// <summary>Statuso aprodorojimas</summary>
	public Func<HttpContext, Task>? StatusHandler { get; set; }

	/// <summary>Klaidų aprodorojimas</summary>
	public Func<HttpContext, IExceptionHandlerFeature, Task> ErrorHandler { get; set; } = async (ctx, ex) => await ctx.Response.WriteAsync("Error...");

	private async Task HandleError(HttpContext ctx) {
		var ex = ctx.Features.Get<IExceptionHandlerFeature>();
		if (StatusHandler is not null && ctx.Items.TryGetValue("Err", out var obj) && obj is int val) {
			switch (val) {
				case 401: await ctx.Response.E401(); break;
				case 403: await ctx.Response.E403(); break;
				case 404: await ctx.Response.E404(); break;
				default: ctx.Response.StatusCode = val; break;
			}
			await StatusHandler(ctx);
		}
		else if (ex is not null && ex.Error is not null) await ErrorHandler(ctx, ex);
	}
}

