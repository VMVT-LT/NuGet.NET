using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vmvt.EntraLogin.Models;

namespace Vmvt.EntraLogin;


/// <summary>Entra ID objektų plėtiniai</summary>
public static class Extensions {
	/// <summary>string->int</summary><param name="dt"></param><param name="_default"></param><returns></returns>
	public static int ToInt(this string dt, int _default = 0) => !string.IsNullOrWhiteSpace(dt) && int.TryParse(dt, out var ret) ? ret : _default;
	/// <summary>string->int</summary><param name="dt"></param><returns></returns>
	public static int? ToIntN(this string dt) => !string.IsNullOrWhiteSpace(dt) && int.TryParse(dt, out var ret) ? ret : null;


	/// <summary></summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	/// <param name="dict"></param>
	/// <param name="act"></param>
	public static void RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict, Func<TValue, bool> act) where TKey : notnull {
		var rm = new List<TKey>();
		foreach (var i in dict) if (act(i.Value)) rm.Add(i.Key);
		foreach (var i in rm) dict.Remove(i, out _);
	}


	/// <summary>Enum to prompt query</summary>
	/// <param name="prompt"></param>
	/// <returns></returns>
	public static string ToPrompt(this AuthPrompt prompt) =>
		prompt switch {
			AuthPrompt.Login => "&prompt=login",
			AuthPrompt.None => "&prompt=none",
			AuthPrompt.Select => "&prompt=select_account",
			AuthPrompt.Consent => "&prompt=consent",
			_ => ""
		};

	/// <summary>Enum to prompt query</summary>
	/// <param name="prompt"></param>
	/// <returns></returns>
	public static bool IsRequested(this AuthPrompt prompt) => prompt is AuthPrompt.Login or AuthPrompt.Select or AuthPrompt.Consent;

	/// <summary>Enum to prompt query</summary>
	/// <param name="prompt"></param>
	/// <returns></returns>
	public static AuthPrompt ToPrompt(this string prompt) =>
		prompt.ToLower() switch {
			"login" => AuthPrompt.Login,
			"select" => AuthPrompt.Select,
			"consent" => AuthPrompt.Consent,
			"ldap" => AuthPrompt.Ldap,
			_ => AuthPrompt.None
		};

	/// <summary>Pridėti adresui "query" parametrą</summary>
	/// <param name="url">Adreso tekstas</param>
	/// <param name="key">Parametras</param>
	/// <param name="value">Reikšmė</param>
	/// <returns></returns>
	public static string AddQueryParam(this string url, string key, string value) {
		var uri = new UriBuilder(url);
		var qry = System.Web.HttpUtility.ParseQueryString(uri.Query);
		qry[key] = value; uri.Query = qry.ToString();
		return uri.ToString();
	}


	/// <summary>Autorizacijos užklausos formos formatavimas</summary>
	/// <param name="form">Užklausos forma</param>
	/// <returns></returns>
	public static AuthForm GetAuthForm(this IFormCollection form) {
		form.TryGetValue("state", out var state); form.TryGetValue("code", out var cd);
		form.TryGetValue("user", out var user); form.TryGetValue(state.ToString(), out var pass);
		if (string.IsNullOrEmpty(pass)) form.TryGetValue("pass", out pass);
		var dt = new AuthForm() { State = state, Code = cd, User = user, Pass = pass, Request = Guid.TryParse(state, out var rq) ? rq : null };
		if (string.IsNullOrEmpty(dt.State))
			throw new EntraException(271, "GetAuthForm", "Neteisingai suformuotas atsakymas", form.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault()));

		if (string.IsNullOrEmpty(dt.Code)) {			
			if (form.TryGetValue("error", out var error)) { // OAuth2 klaidų valdymas
				switch (error) {
						// Bandom prisijungti (login_required - dažnas dėl naujos sesijos) 
					case "login_required": dt.Redirect = AuthPrompt.Login; return dt;
						// Dažnas reikalavimas dėl pass pasikeitimo ar MFA
					case "interaction_required": dt.Redirect = AuthPrompt.Select; return dt;
						// Registruojam klaidą:
					case "consent_required": dt.Redirect = AuthPrompt.Consent; break;
					case "access_denied": break;
					case "invalid_request": break;
					case "unauthorized_client": break;
					case "invalid_scope": break;
					case "unsupported_response_type": break;
					case "redirect_uri_mismatch": break;
						// Servisas neveikia - peradresuojam į LDAP:
					case "method_not_allowed":
					case "temporarily_unavailable":
					case "server_error": dt.Redirect = AuthPrompt.Ldap; break;
				}
				dt.ErrCode = error;
				dt.ErrDescr = form.TryGetValue("error_description", out var dsc) ? dsc.ToString() : null;
			}

			else if (string.IsNullOrEmpty(dt.User) || string.IsNullOrEmpty(dt.Pass))
				throw new EntraException(272, "GetAuthForm", "Nenurodytos prisijungimo detalės", form.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault()));
			else dt.Ldap = true;
		}
		else if (dt.Request is null)
			throw new EntraException(273, "GetAuthForm", "Neteisinga prisijungimo užklausa", form.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault()));

		return dt;
	}

	private static readonly Random Rnd = new();
	private const string RndChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
	/// <summary>Sesijos rakto generavimas</summary>
	/// <param name="length"></param>
	/// <returns></returns>
	public static string RandomStr(int length = 60) {
		var sb = new System.Text.StringBuilder();
		for (var i = 0; i < length; i++) { var c = RndChars[Rnd.Next(0, RndChars.Length)]; sb.Append(c); }
		return sb.ToString();
	}





	/// <summary>Vartotojo sesijos gavimas</summary>
	/// <param name="ctx"></param><param name="prm"></param><returns></returns>
	public static ValueTask<UserSession?> SessionService(HttpContext ctx, ParameterInfo prm) {
		if (ctx.GetAuth(out var usr) && usr is not null) return ValueTask.FromResult<UserSession?>(usr);
		if (prm.ParameterType == typeof(UserSession))
			if (new NullabilityInfoContext().Create(prm).WriteState is NullabilityState.Nullable)
				return ValueTask.FromResult<UserSession?>(null);
		ctx.Items["Err"] = 401;
		throw new EntraException(275, "Entra.Session", "Vartotojo sesija nerasta");
	}


	/// <summary>Pridėti EntraAuth kaip web servisą</summary>
	/// <param name="svc"></param>
	/// <param name="cfg"></param>
	/// <exception cref="Exception"></exception>
	public static IServiceCollection AddEntraService(this IServiceCollection svc, EntraCfg cfg) => svc.AddSingleton(new EntraAuth(cfg));

}



/// <summary>Sesijos plėtiniai</summary>
public static class SessionExtensions {

	/// <param name="ctx"></param>
	/// <returns>Gauti vartotojo informaciją</returns>
	public static UserSession? GetUser(this HttpContext ctx) => ctx.Items.TryGetValue("User", out var usr) && usr is UserSession uss ? uss : null;

	/// <summary>Gauti vartotojo autorizaciją</summary>
	/// <param name="ctx"></param><param name="usr"></param>
	public static bool GetAuth(this HttpContext ctx, out UserSession usr) {
		var entra = ctx.RequestServices.GetRequiredService<EntraAuth>();
		if (entra is not null && entra.GetAuth(ctx, out usr)) return true;
		usr = new(); return false;
	}

	/// <summary>Gauti vartotojo autorizaciją</summary>
	/// <param name="ctx"></param><param name="usr"></param>
	public static bool GetAuth(this EndpointFilterInvocationContext ctx, out UserSession usr) => ctx.HttpContext.GetAuth(out usr);

	/// <summary>Pariktinti ar vartotojas turi reikalaujamą rolę</summary>
	/// <param name="sess">Vartotojo sesija</param><param name="roles">Rolės</param>
	public static bool MatchRole(this UserSession sess, string[] roles) {
		foreach (string i in roles) if (sess.Roles.Contains(i)) return true; return false;
	}

	/// <summary>Pariktinti ar vartotojas turi reikalaujamą rolę</summary>
	/// <param name="sess">Vartotojo sesija</param><param name="role">Rolė</param>
	public static bool MatchRole(this UserSession sess, string role) => sess.Roles.Contains(role);

	/// <summary>Gauti papildomus sesijos duomenis</summary>
	/// <typeparam name="T">Duomenų tipas</typeparam>
	/// <param name="sess"></param>
	/// <returns></returns>
	public static T? GetData<T>(this UserSession sess) where T : class => sess?.Data as T;
}


/// <summary>Graph modelių plėtiniai</summary>
public static class GraphExtensions {

	/// <summary>Gauti Graph vartotojo detales</summary>
	/// <param name="usr">Graph vartotojo objektas</param>
	/// <returns></returns>
	public static UserDetails GetUser(this GraphUser usr) => new() {
		ID = usr.ID, Login = usr.Login, DisplayName = usr.DisplayName, FirstName = usr.FirstName, LastName = usr.LastName, JobTitle = usr.JobTitle,
		Department = usr.Department, Office = usr.Office, Email = usr.Email, LoginAD = usr.LoginAD, Mobile = usr.Mobile, Phone = usr.Phone
	};

	/// <summary>Gauti Graph vartotojo vadovą</summary>
	/// <param name="usr">Graph vartotojo objektas</param>
	/// <returns></returns>
	public static UserBase? GetManager(this GraphUser usr) => usr.Manager is null ? null : new() {
		Login = usr.Manager.Login, Department = usr.Manager.Department, DisplayName = usr.Manager.DisplayName, JobTitle = usr.Manager.JobTitle
	};


	/// <summary>Vartotojo EntraID sesijos inicijavimas</summary>
	/// <param name="usr">Graph vartotojo informacija</param>
	/// <param name="cfg">EntraLogin konfigūracija</param>
	/// <returns></returns>
	public static UserSession ToSession(this GraphUser usr, EntraCfg cfg) {
		var ret = new UserSession() {
			User = usr.GetUser(), Manager = usr.GetManager(),
			SSID = Extensions.RandomStr(cfg.Session.KeyLength),
			Session = new Session() {
				Expire = DateTime.UtcNow.AddSeconds(cfg.Session.Expire),
				Extend = DateTime.UtcNow.AddSeconds(cfg.Session.Extend),
				Provider = "EntraID",
			}
		};
		if (usr.Groups is not null) {
			foreach (var grp in usr.Groups) {
				if (cfg.GroupList is null) ret.Groups.Add(grp.Name ?? grp.Id.ToString() ?? "");
				else
					foreach (var rle in cfg.GroupList)
						if (grp.Id == rle.ID) {
							if (rle.Roles is not null) ret.Roles.UnionWith(rle.Roles);
							var nm = rle.Name ?? grp.Name; if (nm is not null) ret.Groups.Add(nm);
							break;
						}
			}
		}
		return ret;
	}
}


/// <summary>Ldap modelių plėtiniai</summary>
public static class LdapExtensions {
	/// <summary>Gauti Ldap vartotojo detales</summary>
	/// <param name="usr">Ldap vartotojo objektas</param>
	/// <returns></returns>
	public static UserDetails GetUser(this LdapUser usr) => new() {
		Login = usr.Login, DisplayName = usr.DisplayName, FirstName = usr.FirstName, LastName = usr.LastName, JobTitle = usr.JobTitle,
		Department = usr.Department, Office = usr.Office, Email = usr.Email, LoginAD = usr.LoginAD, Mobile = usr.Mobile, Phone = usr.Phone
	};

	/// <summary>Gauti Ldap vartotojo vadovą</summary>
	/// <param name="usr">Ldap vartotojo objektas</param>
	/// <returns></returns>
	public static UserBase? GetManager(this LdapUser usr) => usr.Manager is null ? null : new() {
		Login = usr.Manager.Login, Department = usr.Manager.Department, DisplayName = usr.Manager.DisplayName, JobTitle = usr.Manager.JobTitle
	};

	/// <summary>Vartotojo EntraID sesijos inicijavimas</summary>
	/// <param name="usr">Ldap vartotojo informacija</param>
	/// <param name="cfg">EntraLogin konfigūracija</param>
	/// <returns></returns>
	public static UserSession ToSession(this LdapUser usr, EntraCfg cfg) {
		var ret = new UserSession() {
			User = usr.GetUser(), Manager = usr.GetManager(),
			SSID = Extensions.RandomStr(cfg.Session.KeyLength),
			Session = new Session() {
				Expire = DateTime.UtcNow.AddSeconds(cfg.Session.Expire),
				Extend = DateTime.UtcNow.AddSeconds(cfg.Session.Extend),
				Provider = "LDAP",
			}
		};
		if (usr.Groups is not null) {
			foreach (var grp in usr.Groups) {
				if (cfg.GroupList is null) ret.Groups.Add(grp.Name ?? grp.DN ?? "");
				else
					foreach (var rle in cfg.GroupList)
						if (grp.Name == (rle.AD ?? rle.Name)) {
							if (rle.Roles is not null) ret.Roles.UnionWith(rle.Roles);
							var nm = rle.Name ?? grp.Name; if (nm is not null) ret.Groups.Add(nm);
							break;
						}
			}
		}
		return ret;
	}

}






/// <summary>strint[]->string</summary>
public class FirstConverter : JsonConverter<string> {
	/// <inheritdoc/>
	public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.String) return reader.GetString();
		string? val = null;
		if (reader.TokenType == JsonTokenType.StartArray)
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) val ??= reader.GetString();
		return val;
	}
	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter wrt, string val, JsonSerializerOptions o) { wrt.WriteStringValue(val); }
}
