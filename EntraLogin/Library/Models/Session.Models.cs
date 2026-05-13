using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Vmvt.EntraLogin.Models;

/// <summary>Vartotojo informacija</summary>
public class UserData {
	/// <summary>Vartotojo informacija</summary>
	public UserDetails? User { get; set; } = null!;
	/// <summary>Tiesioginis vadovas</summary>
	public UserBase? Manager { get; set; }
	/// <summary>Vartotojo rolės</summary>
	public HashSet<string> Roles { get; set; } = [];
	/// <summary>Vartotojo teisės</summary>
	public HashSet<string> Groups { get; set; } = [];
	/// <summary>Papildomi sesijos duomenys</summary>
	public object? Data { get; set; } = null;
}

/// <summary>Vartotojo sesijos modelis</summary>
public class UserSession : UserData {
	/// <summary>Vartotojo prisijungimo vardas</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public string Login => User?.Login ?? string.Empty;
	/// <summary>Sesijos raktas</summary>
	public string SSID { get; set; } = "";
	/// <summary>Sesijos baziniai duomenys</summary>
	public Session Session { get; set; } = null!;

	/// <summary></summary><param name="ctx"></param><param name="prm"></param>
	public static async ValueTask<UserSession?> BindAsync(HttpContext ctx, ParameterInfo prm) => await Extensions.SessionService(ctx, prm);

}


/// <summary>Sesijos informacija</summary>
public class Session {
	/// <summary>Sesijos pratęsimo laikas</summary>
	public DateTime Extend { get; set; }
	/// <summary>Sesijos pabaigos laikas</summary>
	public DateTime Expire { get; set; }
	/// <summary>Sesijos atnaujinimų skaičius</summary>
	public int Extended { get; set; }
	/// <summary>Sesijos sukurimo laikas</summary>
	public DateTime StartTime { get; set; } = DateTime.UtcNow;
	/// <summary>Prisijungimo sistema</summary>
	public string? Provider { get; set; }
	/// <summary>Vartotojo naršyklės informacija</summary>
	public string? UserAgent { get; set; }
	/// <summary>IP Adresas</summary>
	public string? UserIP { get; set; } 
}





/// <summary>Vartotojo grupių identifikavimas</summary>
public class GroupMap {
	/// <summary>EntraID grupės identifikatorius</summary>
	public Guid ID { get; set; }
	/// <summary>AD grupės pavadinimas (Ldap)</summary>
	public string? AD { get; set; }
	/// <summary>Grupės pavadinimas</summary>
	public string? Name { get; set; }
	/// <summary>Priskirtos rolės</summary>
	public List<string>? Roles { get; set; }

}


///// <summary>Sesijos pasikeitimo įrašas</summary>
///// <param name="user"></param>
///// <param name="type"></param>
//public class SessionLog(UserSession user, SessionLogType type) {
//	/// <summary>Įrašo tipas</summary>
//	public SessionLogType Type { get; set; } = type;
//	/// <summary>Vartotojo detalės</summary>
//	public UserSession User { get; set; } = user;
//}

/// <summary>Sesijos pasikeitimo tipas</summary>
public enum SessionLogType {
	/// <summary>Nauja sesija</summary>
	Login, 
	/// <summary>Sesijos pratęsimas</summary>
	Extend,
	/// <summary>Sesijos galiojimo pabaiga</summary>
	Expire,
	/// <summary>Vartotojo atsijungimas</summary>
	Logout,
	/// <summary>Sesijos pašalinimas (Extended)</summary>
	Remove
}