using Vmvt.EntraLogin.Models;
using Novell.Directory.Ldap;

namespace Vmvt.EntraLogin;

/// <summary>LDAP prisijungimas</summary>
/// <param name="cfg">Konfigūracijos objektas</param>
public class LdapAuth(EntraCfgLdap cfg) {
	/// <summary>LDAP konfigūracija</summary>
	public EntraCfgLdap Config { get; } = cfg;

	/// <summary>Prisijungti su vartotoju</summary>
	/// <param name="upn">Prisijungimo vardas (userPrincipalName)</param>
	/// <param name="pass">Vartotojo slaptažodis</param>
	/// <returns>Vartotojo detalės</returns>
	public async Task<LdapUser> Login(string upn, string pass) {
		try {
			using var conn = new LdapConnection();
			LdapUser? ret = null;
			try {
				await conn.ConnectAsync(Config.Host, Config.Port);
				conn.Constraints = new() { ReferralFollowing = true };
				await conn.BindAsync(upn, pass);
				if (conn.Bound) {
					ret = await GetUser(conn, Config.BaseDN, conn.AuthenticationDn, Config.UserSelect);
				}
				else throw new EntraException(281, "Ldap.Login", "Vartotojas neatpažintas");
			} catch (LdapException ex) {
				throw ex.ResultCode switch {
					49 => new EntraException(282, "Ldap.Login", "Neteisingi prisijungimo duomenys", ex),
					32 => new EntraException(283, "Ldap.Login", "Vartotojas nerastas", ex),
					_ => new EntraException(284, "Ldap.Login", ex.Message, ex),
				};
			} finally { conn.Disconnect(); }

			if (ret is null) 
					throw new EntraException(285, "Ldap.Login", "Prisijungimas negalimas");
			return ret;
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(286, "Ldap.Login", ex.Message, ex);
		}
	}

	private async Task<LdapUser> GetUser(LdapConnection conn, string dn, string? upn = null, string[]? attr = null) {
		try {
			var srh = await conn.SearchAsync(dn,
				upn is null ? LdapConnection.ScopeBase : LdapConnection.ScopeSub,
				upn is null ? "(objectClass=*)" : $"({Config.Search}={upn})",
				attr ?? Config.UserDefault, false
			);

			if (!await srh.HasMoreAsync())
				throw new EntraException(287, "Ldap.User", "Vartotojas nerastas");

			var usr = await srh.NextAsync();
			return await MapEntry<LdapUser>(conn, usr);
		} catch (EntraException) { throw; } catch (Exception ex) {
			throw new EntraException(288, "Ldap.User", ex.Message, ex);
		}
	}

	private async Task<T> MapEntry<T>(LdapConnection conn, LdapEntry entry) where T : new() {
		try {
			var ret = new T();
			var props = typeof(T).GetProperties();
			var set = entry.GetAttributeSet();

			foreach (var prop in props) {
				LdapProp? attr = prop.GetCustomAttributes(typeof(LdapProp), true).FirstOrDefault() as LdapProp;
				if (attr is not null && set.TryGetValue(attr.Name, out var val) && val is not null) {
					if (attr.Name == "manager" && prop.PropertyType == typeof(LdapUserBase))
						prop.SetValue(ret, await GetUser(conn, val.StringValue));
					else if (attr.Name == "memberOf" && prop.PropertyType == typeof(List<LdapDN>)) {
						var grp = new List<LdapDN>(); foreach (var dn in val.StringValueArray) grp.Add(new(dn));
						prop.SetValue(ret, grp);
					}
					else if (prop.PropertyType == typeof(List<string>)) prop.SetValue(ret, val.StringValueArray.ToList());
					else if (prop.PropertyType == typeof(string)) prop.SetValue(ret, val.StringValue);
				}
			}
			return ret;
		} catch (Exception ex) {
			throw new EntraException(289, "Ldap.Entry", ex.Message, ex);
		}
	}

	/// <summary>Ldap prisijungimo formos šablonas</summary>
	public const string LoginForm = @"<!DOCTYPE html><html lang=""lt""><head>
	<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=0.8"">
	<title>Prisijungimas</title>
	<link href=""https://cdn.vmvt.lt/fonts/axiforma.css"" rel=""stylesheet"">
	<style>
		*{font-family:axiforma,arial;} body{display:flex;justify-content:center;margin-top:50px;} p{font-size:15px;margin:15px 0 0 0;}
		form{border:1px solid #ccc;padding:20px;border-radius:8px;width:320px;display:flex;flex-direction: column;}
		div{margin-bottom:15px;} label{display:block;line-height:25px;} input{width:100%;padding:10px 15px;margin:2px;box-sizing:border-box;}
		button{width:100%;padding:10px;background:#2A4871;color:white;border:none;cursor:pointer;margin-top:5px; font-size: 15px;}
	</style>
	<script>history.replaceState({},document.title,location.protocol+'//'+location.host+location.pathname);</script>
</head>
<body><form action=""{post}"" method=""post"" autocomplete=""off"">
	<div><label for=""user"">El. pašto adresas</label><input type=""text"" id=""user"" name=""user"" required autocomplete=""off""></div>
	<div><label for=""{state}"">Slaptažodis</label><input type=""text"" name=""{state}"" id=""{state}"" onfocus=""this.type='password'"" autocomplete=""one-time-code"" required></div>
	<button type=""submit"">Prisijungti</button><input type=""hidden"" name=""state"" value=""{state}"">
<p><b>Dėmesio</b>: Šis prisijungimas yra pagalbinis, esant klausimams kreipkitės į IT skyrių.</p>
</form></body></html>";
}




/// <summary>Ldap objekto konstruktorius</summary>
/// <param name="name"></param>
[AttributeUsage(AttributeTargets.Property)]
public class LdapProp(string name) : Attribute {
	/// <summary>Atributo pavadinimas</summary>
	public string Name { get; } = name;
}
