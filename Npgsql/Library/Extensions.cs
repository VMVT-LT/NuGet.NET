using Npgsql;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Vmvt.Npgsql;

/// <summary>Plėtiniai</summary>
public static class Extensions {
	private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
	/// <summary>Gauti tekstinę reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static string? GetStringN(this NpgsqlDataReader rdr, int id) => !rdr.IsDBNull(id) ? rdr.GetString(id) : null;
	/// <summary>Gauti skaitinę reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static int? GetIntN(this NpgsqlDataReader rdr, int id) => !rdr.IsDBNull(id) ? rdr.GetInt32(id) : null;
	/// <summary>Gauti skaitinę reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static long? GetLongN(this NpgsqlDataReader rdr, int id) => !rdr.IsDBNull(id) ? rdr.GetInt64(id) : null;
	/// <summary>Gauti datos reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static DateOnly GetDateOnly(this NpgsqlDataReader rdr, int id) => DateOnly.FromDateTime(rdr.GetDateTime(id));
	/// <summary>Gauti datos reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static DateOnly? GetDateOnlyN(this NpgsqlDataReader rdr, int id) => !rdr.IsDBNull(id) ? DateOnly.FromDateTime(rdr.GetDateTime(id)) : null;
	/// <summary>Gauti datos reikšmę</summary>
	/// <param name="rdr"></param><param name="id"></param><returns></returns>
	public static DateTime? GetDateTimeN(this NpgsqlDataReader rdr, int id) => !rdr.IsDBNull(id) ? rdr.GetDateTime(id) : null;


	/// <summary>Gauti visas įrašo reikšmes kaip objektų masyvą</summary>
	/// <param name="rdr"></param><returns></returns>
	public static object[] GetRow(this NpgsqlDataReader rdr) {
		var cnt = rdr.FieldCount;
		var row = new object[cnt];
		for (int i = 0; i < cnt; i++) row[i] = rdr.GetValue(i);
		return row;
	}

	/// <summary>Gauti objekto klasės parametrų informaciją</summary>
	/// <typeparam name="T">Objekto klasė</typeparam>
	/// <param name="rdr">SQL duomenų skaitytuvas</param>
	/// <returns>Parametrų sąrašas</returns>
	public static DBReadPropInfo[] GetProps<T>(this NpgsqlDataReader rdr) {
		var ret = new List<DBReadPropInfo>();
		var prp = typeof(T).GetProperties();
		var lst = new Dictionary<string, PropertyInfo>();
		foreach (var i in prp) lst[((i.GetCustomAttributes(typeof(DbField), true).FirstOrDefault() as DbField)?.Name ?? i.Name).ToLower()] = i;
		var cnt = rdr.FieldCount;
		for (var i = 0; i < cnt; i++) {
			var name = rdr.GetName(i);
			if (lst.TryGetValue(name.ToLower(), out var nm))
				ret.Add(new(nm) { FieldId = i, FieldName = name, FieldType = rdr.GetDataTypeName(i) });
		}
		return [.. ret];
	}

	/// <summary>Gauti suformuotą objektą iš duomenų įrašo</summary>
	/// <typeparam name="T">Klasė</typeparam>
	/// <param name="rdr">Duomenų bazės skaitytuvas</param>
	/// <param name="props">Duomenų parametrai</param>
	/// <param name="ct"></param>
	/// <returns>Objektas</returns>
	public static async Task<T?> GetObject<T>(this NpgsqlDataReader rdr, DBReadPropInfo[]? props = null, CancellationToken ct = default) where T : new() {
		var t = new T();
		if (!rdr.IsOnRow) if (!await rdr.ReadAsync(ct)) return default;
		props ??= rdr.GetProps<T>();
		foreach (var i in props) {
			var f = i.FieldId;
			if (await rdr.IsDBNullAsync(f, ct)) continue;

			var val = i.Type switch {
				_ when i.Type == typeof(string) => rdr.GetString(f),
				_ when i.Type == typeof(int) => rdr.GetInt32(f),
				_ when i.Type == typeof(long) => rdr.GetInt64(f),
				_ when i.Type == typeof(float) => rdr.GetFloat(f),
				_ when i.Type == typeof(double) => rdr.GetDouble(f),
				_ when i.Type == typeof(DateTime) => rdr.GetDateTime(f),
				_ when i.Type == typeof(DateOnly) => rdr.GetDateOnly(f),
				_ when i.Type == typeof(Guid) => rdr.GetGuid(f),
				_ when i.List => i.SubType switch {
					_ when i.SubType == typeof(string) => rdr.GetFieldValue<List<string>>(f),
					_ when i.SubType == typeof(int) => rdr.GetFieldValue<List<int>>(f),
					_ when i.SubType == typeof(long) => rdr.GetFieldValue<List<long>>(f),
					_ when i.SubType == typeof(DateTime) => rdr.GetFieldValue<List<DateTime>>(f),
					_ when i.SubType == typeof(DateOnly) => rdr.GetFieldValue<List<DateOnly>>(f),
					_ when i.SubType == typeof(Guid) => rdr.GetFieldValue<List<Guid>>(f),
					_ => i.FieldType == "jsonb" ? JsonSerializer.Deserialize(rdr.GetString(f), i.Type, JsonOpts) : rdr.GetValue(f)
				},
				_ when i.Type.IsClass && i.FieldType == "jsonb" => JsonSerializer.Deserialize(rdr.GetString(f), i.Type, JsonOpts),
				_ => rdr.GetValue(f)
			};
			if (val is not null) i.Setter(t, val);
		}
		return t;
	}


	/// <summary></summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="rdr"></param>
	/// <param name="field"></param>
	/// <param name="jso"></param>
	/// <param name="ct"></param>
	/// <returns></returns>
	public static async Task<T?> GetJsonbObject<T>(this NpgsqlDataReader rdr, int field, JsonSerializerOptions? jso = null, CancellationToken ct = default) where T : new() {
		if (!rdr.IsOnRow) if (!await rdr.ReadAsync(ct)) return default;
		if (!await rdr.IsDBNullAsync(field, ct)) {
			var str = rdr.GetString(field);
			return JsonSerializer.Deserialize<T>(str, jso ?? JsonOpts);
		}
		return default;
	}

	/// <summary></summary>
	/// <param name="rdr"></param>
	/// <param name="name"></param>
	/// <returns></returns>
	public static int GetFieldId(this NpgsqlDataReader rdr, string name) {
		for (int i = 0; i < rdr.FieldCount; i++)
			if (rdr.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
				return i;
		return -1;
	}

	/// <summary>Sukurti naują db importavimą</summary>
	/// <param name="conn">Duomenų šaltinis</param>
	/// <param name="table">Lentelės pavadinimas</param>
	/// <param name="fld">Importuojami laukai</param>
	/// <returns></returns>
	public static DBImport CreateImporter(this NpgsqlDataSource conn, string table, List<string> fld) => new(table, fld, conn);




	/// <summary>Exception -> JSON</summary>
	public static readonly JsonSerializerOptions JsonSafe = new() {
		ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver {
			Modifiers = { JsonIgnoreReflectionTypes }
		}
	};
	private static void JsonIgnoreReflectionTypes(JsonTypeInfo typeInfo) {
		foreach (var property in typeInfo.Properties)
			if (typeof(MemberInfo).IsAssignableFrom(property.PropertyType) || property.Name == "InnerExceptions")
				property.ShouldSerialize = (_, _) => false;
	}
}




/// <summary>Ldap objekto konstruktorius</summary>
/// <param name="name"></param>
[AttributeUsage(AttributeTargets.Property)]
public class DbField(string name) : Attribute {
	/// <summary>Atributo pavadinimas</summary>
	public string Name { get; } = name;
}
