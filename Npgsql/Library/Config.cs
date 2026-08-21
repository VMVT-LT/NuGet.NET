using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

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
	public async Task<T> GetConfig<T>(DB? conn = null) where T : new() {
		if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Group) || string.IsNullOrEmpty(Key) || Values is null || Values.Count == 0)
			throw new("Missing DBConfig values");
		using var dbr = new DBRead(GetSelect(), conn) { PrintQuery = false, PrintParams = false };
		var ret = await dbr.GetJsonbObject<T>(0, opts);


		return ret is null ? throw new("Config object not found") : ret;
	}

	private static readonly JsonSerializerOptions opts = new() { Converters = { new CfgBoolConverter(), new CfgArrayConverter() } };
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

/// <summary></summary>
public class CfgBoolConverter : JsonConverter<bool> {
	/// <summary></summary><param name="reader"></param><param name="typeToConvert"></param><param name="options"></param>
	public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		switch (reader.TokenType) {
			case JsonTokenType.Number:
				if (reader.TryGetInt32(out int num)) return num >= 1; break;
			case JsonTokenType.String:
				var str = reader.GetString(); if (int.TryParse(str, out var numVal)) return numVal >= 1;
				return str?.ToLowerInvariant() switch { "true" or "t" or "y" or "yes" or "taip" => true, _ => false };
			case JsonTokenType.True: return true;
			case JsonTokenType.False: return false;
			case JsonTokenType.Null:
			default: return default;
		}
		return reader.GetBoolean(); // fallback
	}
	/// <summary></summary><param name="writer"></param><param name="value"></param><param name="options"></param>
	public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) { writer.WriteBooleanValue(value);	}
}
/// <summary></summary>
public class CfgArrayConverter : JsonConverter<object> {
	/// <summary></summary><param name="t"></param>
	public override bool CanConvert(Type t) => t == typeof(string[]) || t == typeof(List<string>);
	/// <summary></summary><param name="reader"></param><param name="type"></param><param name="options"></param>
	public override object Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
		var list = new List<string>();
		if (reader.TokenType == JsonTokenType.StartArray) {
			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndArray) break;
				list.Add(reader.GetString() ?? string.Empty);
			}
		}
		else if (reader.TokenType == JsonTokenType.String) {
			var val = reader.GetString();
			if (!string.IsNullOrEmpty(val)) {
				try {
					var parsed = JsonSerializer.Deserialize<List<string>>(val, options);
					if (parsed != null) list.AddRange(parsed);
					else throw new Exception();
				}
				catch {
					foreach (var item in val.Split(',', StringSplitOptions.RemoveEmptyEntries)) list.Add(item.Trim());
				}
			}
		}
		else if (reader.TokenType == JsonTokenType.Null)
			return type.IsArray ? Array.Empty<string>() : new List<string>();
		return type.IsArray ? list.ToArray() : list;
	}
	/// <summary></summary><param name="writer"></param><param name="value"></param><param name="options"></param>
	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) { JsonSerializer.Serialize(writer, value, options); }
}