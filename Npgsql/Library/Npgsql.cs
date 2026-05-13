using Npgsql;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Vmvt.Npgsql;

/// <summary>Duomenų bazės pagalbininkas</summary>
/// <remarks>Inicijuoti naują prisijungimą</remarks>
/// <param name="connStr">DB prisijungimo duomenys</param>
public class DB(string connStr) {
	private static readonly JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
	
	/// <summary>Standartinis DB prisijungimas</summary>
	public static DB Default { get; set; } = new("User ID=postgres; Password=postgres; Server=localhost:5432; Database=postgres;");

	/// <summary>Duomenų bazės prisijungimas</summary>
	public NpgsqlDataSource Source { get; } = new NpgsqlDataSourceBuilder(connStr).EnableDynamicJson().ConfigureJsonOptions(_jso).Build();

	/// <summary>Užklausos nutraukimo laikas</summary>
	public int Timeout { get; set; } = 30;

	/// <summary>Įrašų skaičiaus atnaujinimas (sekundės)</summary>
	public int CountReset { get; set; } = 5000;

	/// <summary>Print query to console</summary>
	public bool Debug { get; set; }
	/// <summary>Print query numbering</summary>
	public long DebugIncr { get; set; }


	private readonly ConcurrentDictionary<string, (int num, DateTime tmo)> Counts = [];
	private DateTime CountClean { get; set; } = DateTime.UtcNow.AddMinutes(5);

	/// <summary>Užklausos įrašų skaičiaus gavimas</summary>
	/// <param name="table">Lentelė</param>
	/// <param name="where">WHERE sąlyga</param>
	/// <param name="param">Užklausos parametrai</param>
	/// <returns>Įrašų skaičius</returns>
	public async Task<int> GetCount(string table, string? where, Dictionary<string, object?>? param = null) {
		var qry = $"{table}{where}";
		if (param?.Count > 0) foreach (var i in param) qry += i.Value?.ToString();
		if (Counts.TryGetValue(qry, out var cnt)) {
			var now = DateTime.UtcNow;
			if (cnt.tmo > now) return cnt.num;
			else if (CountClean < now) {
				CountClean.AddSeconds(CountReset * 2);
				foreach (var i in Counts) if (i.Value.tmo < now) Counts.TryRemove(i.Key, out _);
			}
		}
		using var db = new DBRead($"SELECT count(*) FROM {table}{where};", param, this) { Timeout = Timeout };
		await using var rdr = await db.GetReader();
		if (await rdr.ReadAsync()) return (Counts[qry] = (rdr.GetInt32(0), DateTime.UtcNow.AddSeconds(CountReset))).num;
		return 0;
	}

	/// <summary>Paleisti duomenų bazės užklausą</summary>
	/// <param name="sql">Užklausos tekstas</param>
	/// <param name="param">Parametrai</param>
	/// <returns>Paveiktų įrašų skaičius</returns>
	public async Task<int> Execute(string sql, params (string key, object? val)[] param) {
		using var rdr = new DBRead(sql, this, param) { Timeout = Timeout }; return await rdr.Execute();
	}

	/// <summary>Paleisti duomenų bazės užklausą</summary>
	/// <param name="sql">Užklausos tekstas</param>
	/// <param name="key">Parametras</param>
	/// <param name="val">Parametro reikšmė</param>
	/// <returns>Paveiktų įrašų skaičius</returns>
	public async Task<int> Execute(string sql, string key, object? val) {
		using var rdr = new DBRead(sql, key, val, this) { Timeout = Timeout }; return await rdr.Execute();
	}


	/// <summary>Inicijuoti duomenų skaitytuvą</summary>
	/// <param name="sql">Užklausos tekstas</param>
	public DBRead Read(string sql) => new(sql, this) { Timeout = Timeout };

	/// <summary>Inicijuoti duomenų skaitytuvą</summary>
	/// <param name="sql">Užklausos tekstas</param><param name="param">parametrų rinkinys</param>
	public DBRead Read(string sql, Dictionary<string, object?>? param) => new(sql, param, this) { Timeout = Timeout };

	/// <summary>Inicijuoti duomenų skaitytuvą</summary>
	/// <param name="sql">Užklausos tekstas</param><param name="param">parametrų rinkinys</param>
	public DBRead Read(string sql, params (string key, object? val)[] param) => new(sql, this, param) { Timeout = Timeout };

	/// <summary>Inicijuoti duomenų skaitytuvą</summary>
	/// <param name="sql">Užklausos tekstas</param><param name="key">Parametro pavadinimas</param><param name="value">Parametro reikšmė</param>
	public DBRead Read(string sql, string key, object? value) => new(sql, key, value) { Timeout = Timeout };



	/// <summary>Duomenų įkėlimo konstruktorius</summary>
	/// <param name="table">Lentelės pavadinimas</param><param name="fld">Įkeliami laukai</param>
	public DBImport Import(string table, List<string> fld) => new(table, fld, Source);

	/// <summary>Duomenų puslapiavimo užklausa</summary>
	/// <typeparam name="T"></typeparam><param name="sql"></param>
	public DBPaging<T> Paging<T>(string sql) where T : new() => new(sql, this);

	/// <summary>Paleisti duomenų bazės užklausą</summary>
	/// <param name="sql">Užklausos tekstas</param><param name="param">Parametrai</param><returns>Paveiktų įrašų skaičius</returns>
	public static async Task<int> Exec(string sql, params (string key, object? val)[] param) => await Default.Execute(sql, param);

	/// <summary>Paleisti duomenų bazės užklausą</summary>
	/// <param name="sql">Užklausos tekstas</param><param name="key">Parametras</param><param name="val">Parametro reikšmė</param><returns>Paveiktų įrašų skaičius</returns>
	public static async Task<int> Exec(string sql, string key, object? val) => await Default.Execute(sql, key, val);

}

