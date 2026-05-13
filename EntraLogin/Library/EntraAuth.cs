using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Vmvt.EntraLogin.Models;

namespace Vmvt.EntraLogin;

/// <summary>EntraID autorizacijos modelis</summary>
public class EntraAuth {
	/// <summary>EntraID prisijungimo konfigūracija</summary>
	public EntraCfg Config { get; }
	private HttpClient HClient { get; set; }
	//	private JsonSerializerOptions JsonConv { get; set; } = new();

	private LdapAuth? _ldap;
	private LdapAuth Ldap => _ldap ??= new(Config.Ldap);

	private ConcurrentDictionary<Guid, EntraException> Errors { get; set; } = [];

	/// <summary>Gauti klaidos pranešimą</summary>
	/// <param name="_ref">Klaidos sąsajos numeris</param>
	/// <param name="rem">pašalinti klaidą</param>
	/// <returns></returns>
	public EntraException? GetError(Guid _ref, bool rem = true) {
		var dt = DateTime.UtcNow.AddSeconds(-Config.Session.CleanErrors);
		foreach (var i in Errors) if (i.Value.Date < dt) Errors.TryRemove(i.Key, out _);
		if (rem) return Errors.TryRemove(_ref, out var err) ? err : null;
		else return Errors.TryGetValue(_ref, out var err) ? err : null;
	}


	private DateTime NextLockClean { get; set; } = DateTime.UtcNow;
	private ConcurrentDictionary<string, AuthLock> LockList { get; set; } = new();
	private DateTime NextAuthClean { get; set; } = DateTime.UtcNow;
	private ConcurrentDictionary<Guid, AuthRequest> AuthList { get; set; } = new();
	private DateTime NextLdapClean { get; set; } = DateTime.UtcNow;
	private ConcurrentDictionary<string, AuthRequest> LdapList { get; set; } = new();
	private DateTime NextSessionClean { get; set; } = DateTime.UtcNow;
	private ConcurrentDictionary<string, UserSession> SessionList { get; set; } = new();



	private string GraphUser => $"https://{Config.Graph.Host}/{Config.Graph.Version}/{Config.Graph.GetUser}";
	private string GraphGroups => $"https://{Config.Graph.Host}/{Config.Graph.Version}/{Config.Graph.GetGroups}";
	private string AuthCallback => $"{Config.Endpoints.Host}{Config.Endpoints.Callback}";
	private string AuthRedirect => $"https://{Config.Auth.Host}/{Config.Auth.Tenant}{Config.Auth.UrlAuth}?" +
			$"client_id={Config.Auth.ClientId}&redirect_uri={AuthCallback}&" +
			$"response_mode=form_post&response_type=code&scope={Config.Auth.Scope}";
	private string AuthToken => $"https://{Config.Auth.Host}/{Config.Auth.Tenant}{Config.Auth.UrlToken}";

	/// <summary>EntraID Autorizacijos sukūrimas</summary>
	/// <param name="cfg">Konfigūracijos objektas</param>
	public EntraAuth(EntraCfg cfg) {
		Config = cfg;
		HClient = new() { Timeout = new TimeSpan(0, 0, 30) };
		//		JsonConv.Converters.Add(new FirstConverter());
	}


	/// <summary>Pagrindinis vartotojo prisijungimo iniciavimas</summary>
	/// <param name="ctx"></param>
	/// <param name="ret">Atgalinio nukreipimo adresas</param>
	/// <param name="prompt">"Prompt" prisijungimo reikalavimas "login\\select\\consent\\ldap"</param>
	public void Init(HttpContext ctx, string? ret = null, string? prompt = null) => Init(ctx, ret, prompt?.ToPrompt());

	/// <summary>Pagrindinis vartotojo prisijungimo iniciavimas</summary><param name="ctx"></param>
	public void Init(HttpContext ctx) => Init(ctx, null, (AuthPrompt?)null);

	/// <summary>Pagrindinis vartotojo prisijungimo iniciavimas</summary><param name="ctx"></param><param name="ret">Atgalinio nukreipimo adresas</param>
	public void Init(HttpContext ctx, string? ret) => Init(ctx, ret, (AuthPrompt?)null);

	/// <summary>Pagrindinis vartotojo prisijungimo iniciavimas</summary>
	/// <param name="ctx"></param>
	/// <param name="ret">Atgalinio nukreipimo adresas</param>
	/// <param name="prompt">"Prompt" prisijungimo reikalavimas</param>
	public void Init(HttpContext ctx, string? ret = null, AuthPrompt? prompt = null) {
		try {
			if (Config.Ldap.ForceLdap || (prompt == AuthPrompt.Ldap && Config.Ldap.AllowLdap))
				InitLdap(ctx, ret).Wait();
			else
				InitEntra(ctx, ret, prompt);
		} catch (EntraException ex) { HandleError(ctx, ex); } catch (Exception ex) {
			HandleError(ctx, new EntraException(221, "Init", "Prisijungimo iniciavimo klaida", ex) { Fallback = true });
		}
	}


	/// <summary>Inicijuoti Ldap prisijungimą</summary>
	/// <param name="ctx"></param>
	/// <param name="ret"></param>
	private async Task InitLdap(HttpContext ctx, string? ret) {
		try {
			Logout(ctx);
			var auth = new AuthRequest() { IP = LockIP(ctx), Return = ValidReturn(ret), Timeout = DateTime.UtcNow.AddSeconds(Config.Auth.Timeout) };
			LdapList[auth.State] = auth;
			CleanLdap();
			if (string.IsNullOrEmpty(Config.Endpoints.LdapForm))
				await ctx.Response.WriteAsync(LdapAuth.LoginForm.Replace("{state}", auth.State).Replace("{post}", Config.Endpoints.LdapPost));
			else
				ctx.Response.Redirect(Config.Endpoints.LdapForm.AddQueryParam("state", auth.State));
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(222, "Init.Ldap", "Prisijungimo iniciavimo klaida", ex) { Fallback = true };
		}
	}


	/// <summary>Inicijuoti Entra prisijungimą</summary>
	/// <param name="ctx"></param>
	/// <param name="ret">Atgalinio nukreipimo adresas</param>
	/// <param name="prompt">"Prompt" prisijungimo reikalavimas "login\\select\\consent\\ldap"</param>
	private void InitEntra(HttpContext ctx, string? ret, AuthPrompt? prompt = null) {
		try {
			Logout(ctx);
			var pr = prompt ?? (ctx.Request.Cookies.ContainsKey(Config.Session.CookieName) ? null : AuthPrompt.Select);
			ctx.Response.Redirect(GetLoginUrl(ret, pr, LockIP(ctx)));
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(223, "Init.Entra", "Prisijungimo iniciavimo klaida", ex) { Fallback = true };
		}
	}

	private void HandleError(HttpContext ctx, EntraException err) {
		//TODO: Count fallback and move to "Ldap"
		Errors[err.Ref] = err;
		if (Config.ErrorHandler is not null) { ctx.GetAuth(out var usr); err.Session = usr; Config.ErrorHandler(err).Wait(); }
		else {
			if (err.DataObject is Exception ex) err.DataObject = new { ex.Message, ex.StackTrace, ex.Source };
			try { err.DataObject = JsonSerializer.Serialize(err.DataObject); } catch (Exception) { err.DataObject = null; }
			Console.WriteLine($"Error: {err.Source} ({err.Code}) - {err.Message}{(err.DataObject is not null ? "\n" + err.DataObject : "")}");
		}
		ctx.Response.Redirect($"{Config.Endpoints.Error}{err.Ref}");
	}

	/// <summary>Sukurti EntraID prisijungimo nuorodą</summary>
	/// <param name="ret">Peradresavimo po autorizacijos adresas</param>
	/// <param name="prompt">Prisijungimo kartojimas</param>
	/// <param name="ip">Kliento IP</param>
	/// <returns></returns>
	private string GetLoginUrl(string? ret = null, AuthPrompt? prompt = null, string? ip = null) {
		//Sukuriama nauja autorizacija
		var auth = new AuthRequest() {
			IP = ip, Timeout = DateTime.UtcNow.AddSeconds(Config.Auth.Timeout),
			Prompt = prompt?.IsRequested() ?? false, Return = ValidReturn(ret)
		};
		//Senų prisijungimų išvalymas
		CleanAuth();
		//Pridedamas naujas prisijungimas
		AuthList[auth.ID] = auth;
		//Prisijungimo nuoroda
		return $"{AuthRedirect}&state={auth.ID}{prompt?.ToPrompt()}";
	}

	private void CleanAuth() { var now = DateTime.UtcNow; if (NextAuthClean > DateTime.UtcNow) { NextAuthClean = now.AddSeconds(Config.Lock.CleanInterval); foreach (var i in AuthList) if (i.Value.Timeout < now) AuthList.TryRemove(i.Key, out _); } }
	private void CleanLdap() { var now = DateTime.UtcNow; if (NextLdapClean > DateTime.UtcNow) { NextLdapClean = now.AddSeconds(Config.Lock.CleanInterval); foreach (var i in LdapList) if (i.Value.Timeout < now) LdapList.TryRemove(i.Key, out _); } }

	/// <summary>Autorizacijos procesas</summary>
	/// <param name="ctx">Vartotojo užklausa su formos parametrais</param>
	/// <param name="ct"></param>
	/// <returns></returns>
	/// <exception cref="EntraException"></exception>
	public async Task Auth(HttpContext ctx, CancellationToken ct = default) {
		try {
			try {
				var ip = LockIP(ctx);
				var form = ctx.Request.Form.GetAuthForm();
				AuthRequest? auth;

				if (form.Ldap) {
					if (!Config.Ldap.AllowLdap && !Config.Ldap.ForceLdap)
						throw new EntraException(231, "Entra.Auth", "Prisijungimo būdas negalimas", null);
					if (!LdapList.TryRemove(form.State!, out auth))
						throw new EntraException(232, "Entra.Auth", "Nerasta prisijungimo užklausa", null);
				}
				else {
					if (!AuthList.TryRemove(form.Request!.Value, out auth))
						throw new EntraException(233, "Entra.Auth", "Nerasta prisijungimo užklausa", null);
				}

				if (auth.Timeout < DateTime.UtcNow)
					throw new EntraException(234, "Entra.Auth", "Baigėsi užklausos prisijungimo laikas", null);
				if (ip != auth.IP)
					throw new EntraException(235, "Entra.Auth", "Pasikeitė vartotojo adresas", null);


				var ret = auth.Return;

				if (form.ErrCode is not null && Config.ErrorHandler is not null) {
					await Config.ErrorHandler(new EntraException(236, "Entra.Auth", "Vartotojo prisijungimo klaida", new { Error = form.ErrCode, Descr = form.ErrDescr }));
				}

				if (form.Redirect == AuthPrompt.Ldap) {
					await InitLdap(ctx, ret); return;
				}
				else if (form.Redirect is not null) {
					ret = GetLoginUrl(auth.Return, form.Redirect, ip);  
				}
				else if (form.Ldap) {
					var usr = await Ldap.Login(form.User!, form.Pass!);
					if (string.IsNullOrWhiteSpace(usr.Login))
						throw new EntraException(237, "Entra.Auth", "Nerastas vartotojo prisijungimo vardas", null);
					AddCookie(ctx, usr.ToSession(Config));
				}
				else {
					try {
						var at = await GetLoginToken(form.Code!, ct);
						var usr = await GetUser(at.AT!, true, ct);
						if (string.IsNullOrWhiteSpace(usr.Login))
							throw new EntraException(238, "Entra.Auth", "Nerastas vartotojo prisijungimo vardas", null);
						AddCookie(ctx, usr.ToSession(Config));
					} catch (Exception) {
						if (auth.Prompt) throw;
						//Pabandom persiloginti
						ret = GetLoginUrl(auth.Return, AuthPrompt.Select, ip);
					}
				}
				ctx.Response.Redirect(ret);
			} catch (EntraException) { throw; } catch (Exception ex) {
				throw new EntraException(239, "Entra.Auth", "Neteisingai suformuotas atsakymas", ex);
			}
		} catch (EntraException ex) { HandleError(ctx, ex); }
	}

	/// <summary>Atsijungti nuo sistemos</summary>
	/// <param name="ctx"></param>
	/// <param name="r">Atsako adresas</param>
	public void Logout(HttpContext ctx, string? r = null) {
		Logout(ctx);
		ctx.Response.Redirect(ValidReturn(r, Config.Endpoints.PostLogout));
	}
	private void Logout(HttpContext ctx) {
		if (ctx.Request.Cookies.TryGetValue(Config.Session.CookieName, out var ssid)) {
			SessionList.TryRemove(ssid, out var sess);
			if (Config.SessionHandler is not null && sess is not null)
				Config.SessionHandler(SessionLogType.Logout, sess).Wait();
			ctx.Response.Cookies.Delete(Config.Session.CookieName);
		}
	}


	/// <summary>Gauti prieigos rakt1</summary>
	/// <param name="code">Autorizacijos kodas</param>
	/// <param name="ct"></param>
	/// <returns></returns>
	private async Task<AccessToken> GetLoginToken(string code, CancellationToken ct = default) {
		try {
			using var rsp = await HClient.PostAsync(AuthToken,
			new FormUrlEncodedContent([
				new("grant_type", "authorization_code"),
				new("scope", Config.Auth.Scope),
				new("redirect_uri", AuthCallback),
				new("client_id", Config.Auth.ClientId),
				new("client_secret",  Config.Auth.ClientSecret),
				new("code", code),
			]), ct);
			var dt = await rsp.Content.ReadFromJsonAsync<AccessTokenResponse>(ct) ??
				throw new EntraException(241, "Entra.Token", "Prisijungimo klaida (OAuth2)") { Fallback = true };
			if (dt.Error is not null)
				throw new EntraException(242, "Entra.Token", "EntraID prisijungimo klaida", dt.Error);
			if (string.IsNullOrWhiteSpace(dt?.AT))
				throw new EntraException(243, "Entra.Token", "Nerastas prisijungimo raktas", dt);
			return dt;
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(244, "Entra.Token", "Prisijungimo klaida (OAuth2)", ex) { Fallback = true };
		}
	}



	private async Task<T?> GraphGet<T>(string url, string at, CancellationToken ct) {
		try {
			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at);
			using var rsp = await HClient.SendAsync(req, ct);
			return await rsp.Content.ReadFromJsonAsync<T>(ct);
		} catch (Exception ex) {
			throw new EntraException(251, "Graph.Get", "MS Graph klaida", ex) { Fallback = true };
		}
	}

	private async Task<GraphUser> GetUser(string at, bool groups = false, CancellationToken ct = default) {
		try {
			var ret = await GraphGet<GraphUser>(GraphUser, at, ct) ??
				throw new EntraException(252, "Graph.User", "Empty response");
			if (ret.Error is not null)
				throw new EntraException(253, "Graph.User", ret.Error.Message ?? "Klaida", ret.Error);
			if (groups) ret.Groups = await GetGroups(at, null, ct);
			return ret;
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(254, "Graph.User", ex.Message, ex);
		}
	}

	private async Task<List<GraphGroup>> GetGroups(string at, string? next = null, CancellationToken ct = default) {
		try {
			var ret = await GraphGet<GraphResponse<GraphGroup>>(next ?? GraphGroups, at, ct) ??
				throw new EntraException(255, "Graph.Groups", "Tuščias MS graph atsakymas", null);
			if (ret.Error is not null)
				throw new EntraException(256, "Graph.Groups", ret.Error.Message ?? "Nenustatyta klaida", ret.Error);
			if (ret.Data is null)
				throw new EntraException(257, "Graph.Groups", "Nerastos vartotojo grupės", ret) { Fallback = true };

			if (!string.IsNullOrEmpty(ret.NextUrl)) ret.Data.AddRange(await GetGroups(at, ret.NextUrl, ct) ?? []);
			return ret.Data;
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(258, "Graph.Groups", ex.Message, ex);
		}
	}

	private string LockIP(HttpContext ctx) {
		var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
		if (!string.IsNullOrEmpty(ip)) {
			if (!LockList.TryGetValue(ip, out var lck)) { lck = new(); LockList.TryAdd(ip, lck); }
			lock (lck) { lck.LastLock = DateTime.UtcNow; lck.Count++; var dly = Config.Lock.Delay; if (dly > 0) Thread.Sleep(dly); }
			if (NextLockClean < DateTime.UtcNow) {
				NextLockClean = DateTime.UtcNow.AddSeconds(Config.Lock.CleanInterval);
				var cleanint = DateTime.UtcNow.AddSeconds(Config.Lock.CleanDelay);
				var clean = new List<string>();
				foreach (var i in LockList) if (i.Value.LastLock < cleanint) clean.Add(i.Key);
				if (clean.Count > 0)
					foreach (var i in clean)
						if (LockList.TryRemove(i, out var itm) && itm.Count >= Config.Lock.Report)
							throw new EntraException(261, "Entra.Lock", "Pakartotiniai prisijungimai", itm);
			}
		}
		else 
			throw new EntraException(262, "Entra.Lock", "Nerastas IP adresas");
		return ip;
	}




	/// <summary>Vartotojo autorizacijos validavimas</summary>
	/// <param name="ctx"></param>
	/// <returns>T/F jeigu vartotojas prisijungęs</returns>
	public bool GetAuth(HttpContext ctx) => GetAuth(ctx, out _);

	/// <summary>Vartotojo autorizacijos validavimas</summary>
	/// <param name="ctx"></param>
	/// <param name="usr">Gaunamas vartotojas</param>
	/// <returns>T/F jeigu vartotojas prisijungęs</returns>
	public bool GetAuth(HttpContext ctx, out UserSession usr) {
		var ret = ctx.GetUser(); usr = ret ?? new();
		if (ret is not null) return true;
		if (NoSession(ctx)) return false;
		if (!ctx.Request.Cookies.TryGetValue(Config.Session.CookieName, out var ssid) || string.IsNullOrEmpty(ssid)) return SetNoSession(ctx);
		if (!SessionList.TryGetValue(ssid, out ret)) {
			// Išorinis sesijos iniciavimas
			if (Config.RestoreHandler is not null) ret = Config.RestoreHandler(ssid).Result;
			if (ret is not null) { ret.Session.Provider = "Restore"; SessionList[ret.SSID] = ret; }
			else return SetNoSession(ctx);
		}

		if (Config.Session.StrictIP && ret.Session.UserIP != ctx.Connection.RemoteIpAddress?.ToString()) KillSession(ret, "Pasikeitė IP");
		if (Config.Session.StrictUA && ret.Session.UserAgent != ctx.Request.Headers.UserAgent.ToString()) KillSession(ret, "Pasikeitė naršyklė"); 		

		if (ret.Session.Expire < DateTime.UtcNow) { CleanSessions(true); return SetNoSession(ctx); }
		if (ret.Session.Extend < DateTime.UtcNow) ret = ExtendSession(ctx, ret);
		ctx.Items["User"] = usr = ret; return true;
	}

	private static bool SetNoSession(HttpContext ctx) { ctx.Items["NoSession"] = true; return false; }
	private static bool NoSession(HttpContext ctx) => ctx.Items["NoSession"] is true;


	private void KillSession(UserSession sess, string message) {
		SessionList.TryRemove(sess.SSID, out _);
		if (Config.SessionHandler is not null) Config.SessionHandler(SessionLogType.Remove, sess).Wait();
		throw new EntraException(263, "Entra.Block", message);
	}

	private string ValidReturn(string? ret, string? _default = null) => (!string.IsNullOrWhiteSpace(ret) && ret.Length > 0 && ret[0] == '/') ? ret : (_default ?? Config.Endpoints.Return);




	private UserSession ExtendSession(HttpContext ctx, UserSession sess) {
		try {
			sess.Session.Extend = sess.Session.Expire = DateTime.UtcNow.AddSeconds(10); //pratęsti atnaujinimą
			Task.Run(async () => { // Atidėdi senos sessijos pašalinimą
				await Task.Delay(8000); SessionList.TryRemove(sess.SSID, out _);
				if (Config.SessionHandler is not null)
					try {
						Config.SessionHandler(SessionLogType.Remove, sess).Wait();
					} catch (Exception ex) {
						throw new EntraException(264, "Entra.Session", "Nepavyko pratęsti sesijos", ex);
					}
			});
			var ret = new UserSession() {
				User = sess.User, Manager = sess.Manager, Groups = sess.Groups, Roles = sess.Roles,
				Data = sess.Data,
				SSID = Extensions.RandomStr(Config.Session.KeyLength),
				Session = new() {
					Extended = sess.Session.Extended + 1,
					Provider = sess.Session.Provider,
					Expire = DateTime.UtcNow.AddSeconds(Config.Session.Expire),
					Extend = DateTime.UtcNow.AddSeconds(Config.Session.Extend)
				}
			};
			AddCookie(ctx, ret);
			return ret;
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(265, "Entra.Session", "Nepavyko pratęsti sesijos", ex);
		}
	}

	private void CleanSessions(bool force = true) {
		if (force || NextSessionClean < DateTime.UtcNow) {
			NextSessionClean = DateTime.UtcNow.AddSeconds(Config.Session.CleanExpired);
			Task.Run(() => {
				var date = DateTime.UtcNow;
				foreach (var sess in SessionList)
					if (sess.Value.Session.Expire < date) {
						SessionList.TryRemove(sess.Key, out _);
						if (Config.SessionHandler is not null)
							Config.SessionHandler(SessionLogType.Expire, sess.Value).Wait();
					}
			});
		}
	}

	private void AddCookie(HttpContext ctx, UserSession sess) {
		try {
			ctx.Items["User"] = SessionList[sess.SSID] = sess;
			ctx.Response.Cookies.Append(Config.Session.CookieName, sess.SSID, new CookieOptions() {
				SameSite = Config.Debug ? SameSiteMode.None : SameSiteMode.Strict,
				HttpOnly = true, Secure = true,
				Path = Config.Endpoints.Base,
				IsEssential = true,
				Expires = DateTime.UtcNow.AddSeconds(Config.Session.Keep)
			});

			sess.Session.UserIP = ctx.Connection.RemoteIpAddress?.ToString();
			sess.Session.UserAgent = ctx.Request.Headers.UserAgent.ToString();
			if (Config.SessionHandler is not null)
				try {
					Config.SessionHandler(sess.Session.Extended > 0 ? SessionLogType.Extend : SessionLogType.Login, sess).Wait();
				} catch (Exception ex) {
					throw new EntraException(266, "Entra.Session", "Nepavyko inicijuoti sesijos", ex);
				}
			CleanSessions();
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(267, "Entra.Session", "Nepavyko inicijuoti sesijos", ex);
		}
	}
}

