using System.Reflection;
using System.Text.Json;
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

	/// <summary>Konfigūracijos duomenų gavimas</summary>
	/// <typeparam name="T">Duomenų tipas</typeparam>
	/// <param name="conn">DB prisijungimas</param>
	public async Task<T> GetConfig<T>(DB? conn = null) where T : new() {
		if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Group) || string.IsNullOrEmpty(Key) || Values is null || Values.Count == 0)
			throw new("Missing DBConfig values");

		using var dbr = new DBRead($@"SELECT ""{Group}"", ""{Key}"", ""{string.Join("\",\"", Values)}"" FROM {Table} ORDER By {Group};", conn);
		using var rdr = await dbr.GetReader();
		var ret = new T();

		while (await rdr.ReadAsync()) {
			var g = rdr.GetString(0); var k = rdr.GetString(1);
			object? value = null;
			for (int i = 2; i < rdr.FieldCount; i++) {
				if (!rdr.IsDBNull(i)) { value = rdr.GetValue(i); break; }
			}
			if (value is not null) SetProp(ret, g, k, value);
		}
		return ret;
	}

	private static readonly JsonSerializerOptions _jsonOptions = new() {
		PropertyNameCaseInsensitive = true
	};

	private static void SetProp(object root, string group, string key, object value) {
		var gProp = root.GetType().GetProperty(group, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
		if (gProp == null || !gProp.CanRead) return;

		var gObj = gProp.GetValue(root);
		if (gObj == null && gProp.CanWrite) {
			gObj = Activator.CreateInstance(gProp.PropertyType);
			gProp.SetValue(root, gObj);
		}

		if (gObj != null) {
			var keyProp = gObj.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (keyProp != null && keyProp.CanWrite) {
				if (value == null) {
					keyProp.SetValue(gObj, null);
					return;
				}

				if (keyProp.PropertyType.IsAssignableFrom(value.GetType())) {
					keyProp.SetValue(gObj, value);
					return;
				}

				try {
					object val;
					if (value is string strVal && keyProp.PropertyType != typeof(string)) {
						val = JsonSerializer.Deserialize(strVal, keyProp.PropertyType, _jsonOptions)!;
					}
					else if (value is JsonElement jsonElement && keyProp.PropertyType != typeof(JsonElement)) {
						val = jsonElement.Deserialize(keyProp.PropertyType, _jsonOptions)!;
					}
					else {
						var targetType = Nullable.GetUnderlyingType(keyProp.PropertyType) ?? keyProp.PropertyType;
						val = Convert.ChangeType(value, targetType);
					}
					keyProp.SetValue(gObj, val);
				}
				catch {
					// Ignore incompatible values safely
				}
			}
		}
	}
}

/// <summary>IS konfigūracija</summary>
/// <typeparam name="T">Konfiguracijos modelis</typeparam>
/// <remarks>Konfigūracijos inicijavimas</remarks>
/// <param name="cfg">Duomenų lentelės parametrai</param>
/// <param name="conn">DB pridijungimas</param>
public class AppConfig<T>(DBConfig cfg, DB? conn = null) where T : new() {
	/// <summary>Konfigūracijos duomenys</summary>
	public T Data => Reload().GetAwaiter().GetResult();

	private T? Cache { get; set; }
	private DBConfig Cfg { get; set; } = cfg;
	private DB? Conn { get; set; } = conn;
	private DateTime NextReload { get; set; }

	/// <summary>Atnaujinti duomenis</summary>
	/// <param name="force">Priverstinai atnaujinti</param>
	public async Task<T> Reload(bool force = false) {
		var now = DateTime.UtcNow;
		if (force || NextReload < now) {
			NextReload = now.AddSeconds(Cfg.Reload);
			Cache = await Cfg.GetConfig<T>(Conn);
		}
		return Cache ?? new();
	}
}