
using System.Data;

namespace Vmvt.Npgsql;

/// <summary>Konfigūracijos gavimo modelis</summary>
public class DBConfig {
	/// <summary>Lentelės pavadinimas</summary>
	public string? Table { get; set; }
	/// <summary>Grupės laukas</summary>
	public string? Group { get; set; }
	/// <summary>Rakto laukas</summary>
	public string? Key { get; set; }
	/// <summary>Reikšmių laukai</summary>
	public List<string>? Values { get; set; }
	/// <summary>Duomenų atnaujinimo intervalas (sekundės)</summary>
	public int Reload { get; set; } = 300;

	private string GetSelect() => $@"
		SELECT jsonb_object_agg(""{Group}"", group_json) AS result FROM (SELECT ""{Group}"", jsonb_object_agg(""{Key}"", CASE 
		{string.Join(" ", Values!.Select(col => $"WHEN \"{col}\" IS NOT NULL THEN to_jsonb(\"{col}\")"))} END) AS group_json
		FROM {Table} GROUP BY ""{Group}"") sub;";

	/// <summary>Konfigūracijos duomenų gavimas</summary>
	/// <typeparam name="T">Duomenų tipas</typeparam>
	/// <param name="conn">DB prisijungimas</param>
	public async Task<T> GetConfig<T>(DB? conn=null) where T: new() {
		if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Group) || string.IsNullOrEmpty(Key) || Values is null || Values.Count == 0)
			throw new ("Missing DBConfig values");
		using var dbr = new DBRead(GetSelect(), conn);
		var ret = await dbr.GetJsonbObject<T>();

		return ret is null ? throw new("Config object not found") : ret;
	}
}

/// <summary>IS konfigūracija</summary>
/// <typeparam name="T">Konfiguracijos modelis</typeparam>
/// <remarks>Konfigūracijos inicijavimas</remarks>
/// <param name="cfg">Duomenų lentelės parametrai</param>
/// <param name="conn">DB pridijungimas</param>
public class AppConfig<T>(DBConfig cfg, DB? conn = null) where T : new() {
	/// <summary>Konfigūracijos duomenys</summary>
	public T Data => Reload().Result;

	private T? Cache { get; set; }
	private DBConfig Cfg { get; set; } = cfg; 
	private DB? Conn { get; set; } = conn;

	private DateTime NextReload { get; set; }

	/// <summary>Atnaujinti duomenis</summary>
	/// <param name="force">Priverstinai atnaujinti</param>
	public async Task<T> Reload(bool force=false) {
		var now = DateTime.UtcNow;
		if (force || NextReload < now) {
			NextReload = now.AddSeconds(Cfg.Reload);
			Cache = await Cfg.GetConfig<T>(Conn);
		}
		return Cache ?? new();
	}
}
