using Npgsql;
using System.Text;
using System.Text.Json;

namespace Vmvt.Npgsql;

/// <summary>Duomenų puslapiavimo užklausa</summary>
/// <typeparam name="T"></typeparam>
/// <remarks>Puslapiavimo užklausos konstruktorius</remarks>
/// <param name="db">Duomenų bazės prisijungimas</param>
/// <param name="table">Lentelės pavadinimas</param>
public class DBPaging<T>(string table, DB? db = null) where T : new() {
	private DB Db { get; set; } = db ?? DB.Default;
	/// <summary>Lentelės pavadinimas</summary>
	public string Table { get; set; } = table;
	/// <summary>Užklausos ribojimas</summary>
	public T? Where { get; set; }
	/// <summary>Užklausos ribojimas</summary>
	public string? WhereAdd { get; set; }
	/// <summary>Paieškos laukas</summary>
	public string? Search { get; set; }
	/// <summary>Paieška prasideda fraze</summary>
	public string? SearchSort { get; set; }
	/// <summary>Paieška prasideda fraze</summary>
	public bool StartsWith { get; set; }
	/// <summary>Puslapio dydis</summary>
	public int Limit { get; set; } = 50;
	/// <summary>Maksimalus įrašų kiekis puslapyje</summary>
	public int MaxLimit { get; set; } = 1000;
	/// <summary>Puslapis</summary>
	public int Page { get; set; } = 1;
	/// <summary>Rikiavimas</summary>
	public string? Sort { get; set; } = "ID";
	/// <summary>Rodomi duomenų laukai</summary>
	public List<string>? Select { get; set; }
	/// <summary>Duomenų lentelės laukai</summary>
	public List<string>? Fields { get; set; }
	/// <summary>Duomenų paieškos laukas</summary>
	public string SearchField { get; set; } = "search";
	/// <summary>Didėjančia tvarka</summary>
	public bool Desc { get; set; }
	/// <summary>Additional parameters</summary>
	public Dictionary<string, object?>? Params { get; set; }
	/// <summary>Imti duomenis iš vieno JSONB lauko</summary>
	public string? JsonField { get; set; }
	/// <summary>Papildomi JSON serializavimo nustatymai</summary>
	public JsonSerializerOptions? JsonOptions { get; set; }
	/// <summary>Get total number of rows</summary>
	public bool Total { get; set; } = true;
	private async Task<(string, Dictionary<string, object?>, int)> Prep() {
		if (Fields is null) { throw new Exception("Missing data fields"); }
		if (Select is null) { if (JsonField is null) throw new Exception("Missing select fields"); else Select = [JsonField]; }
		if (Sort is not null && !Fields.Contains(Sort)) { throw new Exception("Sort not valid"); }
		var srt = $"\"{Sort}\""; var slt = $"\"{string.Join("\",\"", Select)}\"";
		var advs = false;

		string where = ""; var param = Params ?? [];
		var whr = new List<string>(); if (!string.IsNullOrEmpty(WhereAdd)) whr.Add(WhereAdd);
		if (Where is not null) {
			foreach (var i in typeof(T).GetProperties()) {
				var n = i.Name;
				var pv = i.GetValue(Where);
				if (pv is not null) {
					if (!Fields.Contains(n)) { throw new Exception($"Invalid search field '{n}'"); }
					if (i.PropertyType == typeof(int?) && (int)pv == -1) whr.Add($"\"{n}\" is null");
					else { param[$"@{n}"] = pv; whr.Add($"\"{n}\"=@{n}"); }
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(Search)) {
			if (!Fields.Contains("search")) { throw new Exception("Search not available"); }
			if (StartsWith) { whr.Add($"search like @qs||'%'"); param[$"@qs"] = Search; }
			else {
				var qs = Search.ToLower().Replace("  ", " ").Split(" ");
				for (var i = 0; i < qs.Length; i++) {
					var j = qs[i];
					whr.Add($"search like '%'||@s{i}||'%'");
					param[$"@s{i}"] = j;
				}
				slt = $"similarity(search,@srhq){(string.IsNullOrEmpty(SearchSort) ? "" : "*" + SearchSort)} srsiml, {slt}";
				srt = $"srsiml desc" + (srt is null ? "" : "," + srt);
				param["@srhq"] = Search; advs = true;
			}
		}
		if (whr.Count > 0) where = $" WHERE {string.Join(" and ", whr)} ";
		var lmt = Limit > 0 ? $" LIMIT {Limit} OFFSET {(Page - 1) * Limit}" : "";
		var qry = $"SELECT {slt} FROM {Table} {where} {(srt is null ? "" : $"ORDER By {srt} {(Desc ? "Desc" : "Asc")}")} {lmt}";
		if (advs) qry = $"SELECT * FROM ({qry}) WHERE srsiml>0";

		return (qry, param, await Db.GetCount(Table, where, param));
	}


	/// <summary>Vykdyti užklausą</summary>
	/// <returns></returns>
	public async Task<DBPagingResponse<T>> Execute() {
		(string qry, Dictionary<string, object?> param, int cnt) = await Prep();

		var ret = new DBPagingResponse<T>() { Total = Total ? cnt : 0, Page = Page };

		using var db = new DBRead(qry, param, Db);
		using var rdr = await db.GetReader();

		var props = rdr.GetProps<T>();

		var jsfi = !string.IsNullOrEmpty(JsonField) ? rdr.GetFieldId(JsonField) : -1;
		var jsf = jsfi >= 0;

		while (await rdr.ReadAsync()) {
			var itm = jsf ? await rdr.GetJsonbObject<T>(jsfi,JsonOptions) : await rdr.GetObject<T>(props);
			//TODO: Sleep;
			if (itm is not null) ret.Data.Add(itm);
		}
		if (!Total) { ret.Total = ret.Data.Count; }
		return ret;
	}

	/// <summary>Atiduoti JSON rezultatą</summary>
	/// <param name="wrt"></param>
	/// <returns></returns>
	public async Task WriteJson(Stream wrt) {
		(string qry, Dictionary<string, object?> param, int cnt) = await Prep();
		using var db = new DBRead(qry, param);
		await using var rdr = await db.GetReader();
		var props = rdr.GetProps<T>();
		byte[] comma = [(byte)','];
		var iscomma = false; var incr = 0;
		var jsfi = !string.IsNullOrEmpty(JsonField) ? rdr.GetFieldId(JsonField) : -1;
		var jsf = jsfi >= 0;
		await wrt.WriteAsync(Encoding.UTF8.GetBytes($"{{\"page\":{Page},\"total\":{cnt},\"data\":["));
		while (await rdr.ReadAsync()) {
			if (iscomma) await wrt.WriteAsync(comma);
			else iscomma = true; incr++;
			if (jsf) await wrt.WriteAsync(await rdr.GetFieldValueAsync<byte[]>(jsfi));
			else await wrt.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(await rdr.GetObject<T>(props), JsonOptions));
		}
		await wrt.WriteAsync(Encoding.UTF8.GetBytes($"],\"items\":{incr}}}"));
	}


	/// <summary>Atiduoti JSON rezultatą</summary>
	/// <param name="wrt"></param>
	/// <returns></returns>
	public async Task WriteCSV(Stream wrt) {
		(string qry, Dictionary<string, object?> param, _) = await Prep();
		using var conn = await Db.Source.OpenConnectionAsync();
		await using (var cmd = new NpgsqlCommand($"CREATE TEMP TABLE export_data AS ({qry})", conn)) {
			foreach (var p in param) cmd.Parameters.Add(new(p.Key, p.Value ?? DBNull.Value));
			await cmd.ExecuteNonQueryAsync();
		}

		try {
			using var reader = await conn.BeginTextExportAsync("COPY export_data TO STDOUT (FORMAT CSV, HEADER)");
			if (reader is StreamReader streamReader)
				await streamReader.BaseStream.CopyToAsync(wrt);
		}
		finally {
			await using var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS export_data", conn);
			await cmd.ExecuteNonQueryAsync();
		}
	}
}




/// <summary>Duomenų puslapiavimo filtras</summary>
/// <typeparam name="T">Duomenų tipas</typeparam>
public class DBPagingFilter<T> {
	/// <summary>Įrašų kiekis</summary>
	/// <example>20</example>
	public int Top { get; set; } = 50;
	/// <summary>Puslapio numeris</summary>
	/// <example>1</example>
	public int Page { get; set; } = 1;
	/// <summary>Įrašų rikiavimas</summary>
	/// <example>ID</example>
	public string Order { get; set; } = "ID";
	/// <summary>Paeiškos frazė</summary>
	public string? Search { get; set; }
	/// <summary>Rikiavimo tvarka</summary>
	/// <example>false</example>
	public bool Desc { get; set; }
	/// <summary>Filtras</summary>
	public T? Filter { get; set; }
}

/// <summary>Duomenų puslapiavimo užklausa</summary>
/// <typeparam name="T"></typeparam>
public class DBPagingResponse<T> {
	/// <summary>Grąžinamų duomenų kiekis</summary>
	/// <example>10</example>
	public int Items => Data.Count;
	/// <summary>Bendras duomenų kiekis</summary>
	/// <example>100</example>
	public int Total { get; set; }
	/// <summary>Puslapis</summary>
	/// <example>1</example>
	public int Page { get; set; }
	/// <summary>Duomenys</summary>
	public List<T> Data { get; set; } = [];
}
