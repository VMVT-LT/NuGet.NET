using Vmvt.EntraLogin;
using Vmvt.EntraLogin.Models;
using Example;

var builder = WebApplication.CreateBuilder(args);

// Sukuriamas EntraCfg pagal appsettings.json
var cfg = builder.Configuration.GetSection("Config").Get<EntraCfg>() ?? new();

// Prisijungimų, klaidų ir sesijos informacijos apdorojimas
cfg.SessionHandler = Auth.SessionHandler;
cfg.RestoreHandler = Auth.RestoreHandler;
cfg.ErrorHandler = Auth.ErrorHandler;

// Išjungiama "SameSite" slapuko apsauga
cfg.Debug = true;

// Pridedamas EntraAuth servias ir tiesioginis sesijos perdavimas
builder.Services.AddEntraService(cfg);

// ARBA: EntraAuth Objektas gali būti saugomas atskirai arba perduodamas tiesiogiai į API
// builder.Services.AddSingleton(new EntraAuth(cfg));

var app = builder.Build();


// Prisijungimo iniciavimas
// "r" - Nukreipimo adresas po autorizacijos
// "p" - "Prompt" prisijungimo reikalavimas "login\select\consent\ldap"
//                galima naudoti ir "Enum" ar InitEntra/InitLdap atskirai
app.MapGet("/auth/login", (HttpContext ctx, EntraAuth entra, string? r = null, string? p = null) => entra.Init(ctx, r, p));

// Pagrindinis prisijungimo API
//  priimamas Form POST metodas (application/x-www-form-urlencoded)
app.MapPost("/auth/login", async (HttpContext ctx, EntraAuth entra, CancellationToken ct) => await entra.Auth(ctx, ct));

// Vartotojo atsijungimas
// "r" - Nukreipimo adresas
app.MapGet("/auth/logout", (HttpContext ctx, EntraAuth entra, string? r = null) => entra.Logout(ctx, r) );

// Klaidos gavimas pagal kodą
app.MapGet("/auth/error/{guid}", (HttpContext ctx, EntraAuth entra, Guid guid) => entra.GetError(guid)?.ToError());

// Vartotojo informacija (su sesijos validavimu)
app.MapGet("/api/user", (HttpContext ctx, EntraAuth entra) => {
	if (entra.GetAuth(ctx, out var usr)) {
		// Vartotojas prisijungęs
		return Results.Ok(usr);
	}
	return Results.Unauthorized();
});



// Supaprastintas prisijungimo gavimas
app.MapGet("/api/members", (HttpContext ctx) => $"Tik prisijungusiems ({ctx.GetUser()?.User?.Login})" ).RequireLogin();
// Supaprastintras rolių tikrinimas
app.MapGet("/api/secret", () => "Paslaptis...").RequireRole("admins");

// Vartotojo informacija (tiesiogiai)
app.MapGet("/api/user2", (UserSession usr) => usr).RequireLogin();
// UserSession - be vartotojo informacijos atiduoda klaidą (throw exception), kuri suvartoja daug resursų
//               reiktų "RequireLogin" filtrų ar kitų "AddEndpointFilter" kurie atmeta užklausą be papildomos klaidos
// UserSession? - be vartotojo atiduos "null", kuris daug paprasčiau veikia.

app.Run();


namespace Example {
	public static class Auth {

		/// <summary>Prisijungimo reikalavimo pavyzdys</summary>
		/// <param name="builder"></param>
		/// <returns></returns>
		public static RouteHandlerBuilder RequireLogin(this RouteHandlerBuilder builder) =>
			builder.AddEndpointFilter(async (ctx, next) => {
				// Gauti EntraAuth objektą (galima static prop naudoti)
				var entra = ctx.HttpContext.RequestServices.GetRequiredService<EntraAuth>();
				return entra.GetAuth(ctx.HttpContext, out var usr) ? await next(ctx) : Results.Unauthorized();
			});

		/// <summary>Rolės reikalavimo pavyzdys</summary>
		/// <param name="builder"></param>
		/// <param name="roles">Rolių sąrašas</param>
		/// <returns></returns>
		public static RouteHandlerBuilder RequireRole(this RouteHandlerBuilder builder, params string[] roles) =>
			builder.AddEndpointFilter(async (ctx, next) => {
				var entra = ctx.HttpContext.RequestServices.GetRequiredService<EntraAuth>();
				if (entra.GetAuth(ctx.HttpContext, out var usr)) {
					if (usr.Roles.Any(roles.Contains))
						return await next(ctx);
					else
						return Results.StatusCode(403);
				}
				return Results.Unauthorized();
			});


		/// <summary>Prisijungimų apdorojimas</summary>
		/// <param name="type">Prisijungimo tipas</param>
		/// <param name="sess">Vartotojo sesija</param>
		/// <returns></returns>
		public static async Task SessionHandler(SessionLogType type, UserSession sess) {
			switch (type) {
				case SessionLogType.Login: Console.WriteLine($"Login: {sess.User?.Login}"); break;
				case SessionLogType.Extend: Console.WriteLine($"Extended: {sess.User?.Login}"); break;
				case SessionLogType.Remove: Console.WriteLine($"Removed: {sess.User?.Login}"); break;
				case SessionLogType.Expire: Console.WriteLine($"Expired: {sess.User?.Login}"); break;
				case SessionLogType.Logout: Console.WriteLine($"Logout: {sess.User?.Login}"); break;
			}
			await Task.Delay(1000);
		}

		/// <summary>Sesijų inicijamivas</summary>
		/// <param name="ssid">Sesijos raktas</param>
		/// <returns></returns>
		public static async Task<UserSession?> RestoreHandler(string ssid) {
			// Gauti sesiją iš DB
			await Task.Delay(1000);
			// Atiduoti sesiją
			if (ssid == "fake") return new() { SSID = ssid, Session = new() { Expire = DateTime.MaxValue }, User = new() { Login = "fakelogin" } };
			return null;
		}

		/// <summary>Klaidų padorojimas/saugojimas</summary>
		/// <param name="err">Prisijungimo klaidos informacija</param>
		/// <returns></returns>
		public static async Task ErrorHandler(EntraException err) {
			// Aprodoti klaidos informaciją
			Console.WriteLine($"Error: {err.Message} ({err.Source}:{err.Code} ref:{err.Ref})");
			// Užsaugoti klaidą DB
			await Task.Delay(1000);
		}

	}
		
}