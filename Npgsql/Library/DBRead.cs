using Npgsql;
using NpgsqlTypes;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Vmvt.Npgsql;

/// <summary></summary>
public class DBRead : IDisposable {
	private DB Db { get; }
	private NpgsqlCommand Cmd { get; }
	/// <summary>Užklausos nutraukimo laikas</summary>
	public int Timeout { get; set; } = 30;

	/// <summary>Duomenų skaitymas</summary>
	/// <param name="ct"></param>
	/// <returns>Npgsql duomenų skaitytuvas</returns>
	public async Task<NpgsqlDataReader> GetReader(CancellationToken ct = default) {
		if (PrintQuery) Print(); Cmd.CommandTimeout = Timeout;
		return await Cmd.ExecuteReaderAsync(ct);
	}

	/// <summary>Gauti objektą iš duomenų bazės pirmos eilutės</summary>
	/// <typeparam name="T">Objekto klasė</typeparam><param name="ct"></param>
	/// <returns>Suformuotas objektas</returns>
	public async Task<T?> GetObject<T>(CancellationToken ct = default) where T : new() {
		await using var rdr = await GetReader(ct);
		return await rdr.GetObject<T>(null, ct);
	}

	/// <summary>Gauti objektą iš duomenų bazės jsonb įrašo lauko</summary>
	/// <typeparam name="T">Objekto klasė</typeparam><param name="ct"></param>
	/// <param name="field">Lauko numeris</param><param name="jso"></param>
	/// <returns>Suformuotas objektas</returns>
	public async Task<T?> GetJsonbObject<T>(int field = 0, JsonSerializerOptions? jso=null, CancellationToken ct = default) where T : new() {
		await using var rdr = await GetReader(ct);
		return await rdr.ReadAsync(ct) ? (
			jso is null ?
			await rdr.GetFieldValueAsync<T>(field, ct) :
			await rdr.GetJsonbObject<T>(field, jso, ct)
			) : default;
	}



	/// <summary>Gauti duomenų rinkinį</summary>
	/// <typeparam name="T">Duomenų tipas</typeparam><param name="ct"></param>
	/// <returns>Suformuotas sąrašas</returns>
	public async Task<List<T>> GetList<T>(CancellationToken ct = default) where T : new() {
		await using var rdr = await GetReader(ct);
		var ret = new List<T>();
		var prop = rdr.GetProps<T>();
		while (await rdr.ReadAsync(ct)) {
			var dt = await rdr.GetObject<T>(prop, ct);
			if (dt is not null) ret.Add(dt);
		}
		return ret;
	}

	/// <summary>Vykdyti užklausą</summary>
	/// <param name="ct"></param>
	/// <returns>Paveiktų įrašų skaičius</returns>
	public async Task<int> Execute(CancellationToken ct = default) {
		if (PrintQuery) Print(); Cmd.CommandTimeout = Timeout;
		return await Cmd.ExecuteNonQueryAsync(ct);
	}

	/// <summary>Gauti objektą iš duomenų bazės pirmo įrašo</summary>
	/// <typeparam name="T">Objekto klasė</typeparam><param name="ct"></param>
	/// <returns>Suformuotas objektas</returns>
	public async Task<T?> GetScalar<T>(CancellationToken ct = default) {
		if (PrintQuery) Print(); Cmd.CommandTimeout = Timeout;
		var ret = await Cmd.ExecuteScalarAsync(ct);
		if (ret == null || ret == DBNull.Value) return default;
		try { return (T)ret; } catch (Exception) { throw; }
	}

	/// <summary>Gauti objektą iš duomenų bazės pirmo įrašo</summary>
	/// <param name="field">Lauko numeris</param>
	/// <param name="ct"></param>
	/// <returns>Suformuotas objektas</returns>
	public async Task<byte[]?> GetBytes(int field = 0, CancellationToken ct = default) {
		await using var rdr = await GetReader(ct);
		return await rdr.ReadAsync(ct) ? await rdr.GetFieldValueAsync<byte[]>(field, ct) : null;
	}



	/// <summary></summary>
	/// <param name="db"></param>
	/// <param name="sql"></param>
	/// <param name="param"></param>
	public DBRead(string sql, Dictionary<string, object?>? param, DB? db = null) {
		Db = db ?? DB.Default; Cmd = Db.Source.CreateCommand(sql); AddParam(param); PrintParams = PrintQuery = Db.Debug;
	}

	/// <summary></summary>
	/// <param name="db"></param>
	/// <param name="sql"></param>
	/// <param name="param"></param>
	public DBRead(string sql, DB? db, params (string key, object? val)[] param) {
		Db = db ?? DB.Default; Cmd = Db.Source.CreateCommand(sql); AddParam(param); PrintParams = PrintQuery = Db.Debug;
	}

	/// <summary></summary>
	/// <param name="sql"></param>
	/// <param name="param"></param>
	public DBRead(string sql, params (string key, object? val)[] param) {
		Db = DB.Default; Cmd = Db.Source.CreateCommand(sql);AddParam(param); PrintParams = PrintQuery = Db.Debug;
	}

	/// <summary></summary>
	/// <param name="sql"></param>
	/// <param name="db"></param>
	public DBRead(string sql, DB? db = null) {
		Db = db ?? DB.Default; Cmd = Db.Source.CreateCommand(sql); PrintParams = PrintQuery = Db.Debug;
	}


	/// <summary></summary>
	/// <param name="sql"></param>
	/// <param name="key"></param>
	/// <param name="value"></param>
	/// <param name="db"></param>
	public DBRead(string sql, string key, object? value, DB? db = null) {
		Db = db ?? DB.Default; Cmd = Db.Source.CreateCommand(sql); AddParam(key, value); PrintParams = PrintQuery = Db.Debug;
	}


	/// <summary></summary><param name="param"></param>
	public void AddParam(params (string key, object? val)[] param) {
		if (param?.Length > 0) foreach (var (key, val) in param) Cmd.Parameters.Add(new(key, val ?? DBNull.Value));
	}
	/// <summary></summary><param name="param"></param>
	public void AddParam(Dictionary<string, object?>? param) {
		if (param?.Count > 0) foreach (var p in param) Cmd.Parameters.Add(new(p.Key, p.Value ?? DBNull.Value));
	}
	/// <summary>Pridėti užklausos parametrą</summary>
	/// <param name="key">Parametras</param>
	/// <param name="value">Reikšmė</param>
	public void AddParam(string key, object? value) => Cmd.Parameters.Add(new(key, value ?? DBNull.Value));
	/// <summary>Pridėti parametrą su tipu</summary><param name="key"></param><param name="value"></param><param name="type"></param>
	public void AddParam(string key, object? value, NpgsqlDbType type) => Cmd.Parameters.Add(new(key, type) { Value = value ?? DBNull.Value });
	/// <summary>Pridėti Jsonb tipo parametrą</summary><param name="key"></param><param name="value"></param><param name="opts"></param>
	public void AddJsonb(string key, object? value, JsonSerializerOptions? opts = null) => Cmd.Parameters.Add(new(key, NpgsqlDbType.Jsonb) {
		Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value, opts)
	});
	/// <summary>Pridėti Jsonb tipo parametrą su klaidos reikšme</summary><param name="key"></param><param name="value"></param>
	public void AddJsonbError(string key, object? value) => Cmd.Parameters.Add(new(key, NpgsqlDbType.Jsonb) {
		Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value, Extensions.JsonSafe).Replace("\0", "").Replace("\\u0000", "")
	});


	/// <summary>Spausdinti užklausos parametrus konsolėje</summary>
	public bool PrintParams { get; set; }
	/// <summary>Spausdinti užklausos teksta konsolėje</summary>
	public bool PrintQuery { get; set; }
	private void Print() {
		var inc = Db.DebugIncr++;
		Console.WriteLine($"[SQL{inc}]: {Cmd.CommandText}");
		if (PrintParams && Cmd.Parameters.Count > 0)
			Console.WriteLine($"[SQL{inc}]: {JsonSerializer.Serialize(Cmd.Parameters.ToDictionary(x => x.ParameterName, x => x.Value))}");
	}


	private bool IsDisposed;
	/// <summary>Duomenų bazės uždarymo metodas</summary>
	public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
	/// <summary>Duomenų bazės uždarymo metodas</summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing) {
		if (!IsDisposed) {
			if (disposing) {
				try {
					Cmd.Dispose();
				} catch (Exception ex) { Console.WriteLine($"[SQLError{Db.DebugIncr}] Dispose - {ex.Message}\n{ex.StackTrace}"); }
			}
			IsDisposed = true;
		}
	}

}



/// <summary></summary>
public class DBReadPropInfo {
	/// <summary></summary>
	public string? FieldName { get; set; }
	/// <summary></summary>
	public string? FieldType { get; set; }
	/// <summary></summary>
	public int FieldId { get; set; }
	/// <summary></summary>
	public Type Type { get; set; }
	/// <summary></summary>
	public Type? SubType { get; set; }
	/// <summary></summary>
	public PropertyInfo Prop { get; set; }
	/// <summary></summary>
	public bool	List { get; set; }
	/// <summary></summary>
	public Action<object, object> Setter { get; set; }
	/// <summary></summary>
	public DBReadPropInfo(PropertyInfo pr) {
		Prop = pr;
		Type = Nullable.GetUnderlyingType(pr.PropertyType) ?? pr.PropertyType;
		SubType = Type.GetGenericArguments().FirstOrDefault();
		List = SubType is not null && (Type.Name.StartsWith("List") || Type.Name.StartsWith("HashSet"));

		var targetParam = Expression.Parameter(typeof(object), "t");
		var valueParam = Expression.Parameter(typeof(object), "val");
		var assignExpression = Expression.Assign(
			Expression.Property(Expression.Convert(targetParam, pr.DeclaringType!), pr),
			Expression.Convert(valueParam, pr.PropertyType)
		);
		Setter = Expression.Lambda<Action<object, object>>(assignExpression, targetParam, valueParam).Compile();
	}
}

 