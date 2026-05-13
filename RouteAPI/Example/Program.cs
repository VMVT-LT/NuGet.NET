using Example;
using Vmvt.RouteAPI;


// Arba: new RouteApi("[namespace]");
var routes = new RouteApi(
	Example1Route.Route(),
	Example2Route.Route()
) {

	ErrorHandler = async (ctx, err) => {
		ctx.Response.StatusCode = 200;
		await ctx.Response.WriteAsync($"Fatal error: {err.Error.Message}");
	}
};

var app = routes.Build(
	(builder) => {
		var db = builder.Configuration.GetConnectionString("DefaultConnection");

		/* Do something with builder */

	}
);


app.Run();



namespace Example {
	public class Example1Route {
		public static RouteDefinition Route() => new("Example API") {
			Description = "How to use RouteAPI", Tag = "api", Version = "v1",
			Routes = [
				new RouteGroup("Example group 1")
					.Map(new("/api/get", ()=> "Hello Get example"){
						Response = typeof(string),
						Description = "Get \"Hello\" example"
					})
					.Map(new("/api/geterror", () => { throw new ("Get error!"); })),

				new RouteGroup("Example group 2")
					.Map(new("/api/post", PostExample, Method.Post){
						Response = typeof(string),
						Description = "Get \"Hello\" example",
						Params = [
							new("param"){ Description="Parameter (integer)", Type= RouteParamType.Integer, Required=true }
						],
					})
			]
		};

		public static string PostExample(HttpContext ctx) => $"Hello Post example: {ctx.ParamInt("param")}";
	}

	public class Example2Route {
		public static RouteDefinition Route() => new("Example API2") {
			Description = "How to use RouteAPI 2", Tag = "api2", Version = "v1",
			Routes = [
				new RouteGroup("Example group")
					.Map(new("/api2/get",()=> "Hello Get example"){
						Response = typeof(string),
						Description = "Get \"Hello\" example",
						Params = [new("role") { Default="role1", Type=RouteParamType.String, Required=true }],
						Filter = RequireRole("role1","role2"),
						Errors = [401]
					}),
			]
		};


		public static RouteFilter RequireRole(params string[] roles) => async (ctx) => {
			var role = ctx.HttpContext.ParamString("role");
			if (roles.Contains(role)) {
				await Task.Delay(50);
				return null; //Tęsti vykdymą
			}
			return Results.Unauthorized(); //ctx.Response.E401();
		};
	}
}