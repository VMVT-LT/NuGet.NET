using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace Vmvt.Npgsql;

/// <summary>Duomenų įkėlimas</summary>
public class DBImport : IDisposable {
	/// <summary>Lentelės pavadinimas</summary>
	public string Table { get; }
	/// <summary>Išvalyti esamus duomenis</summary>
	public bool Truncate { get; set; }
	/// <summary>Importuojami laukai</summary>
	public List<string> Fields { get; }
	/// <summary>Įrašų skaičius</summary>
	public int RowCount { get; private set; }
	/// <summary>Laukų tipai</summary>
	public NpgsqlDbType?[]? Types { get; set; }
	/// <summary>DB prisijungimo detalės</summary>
	public NpgsqlConnection Conn { get; private set; }
	private NpgsqlBinaryImporter? Imp { get; set; }
	private object Lock { get; set; } = new object();
	private NpgsqlBinaryImporter GetImp => Imp ?? Open();

	private NpgsqlBinaryImporter Open() {
		lock (Lock) {
			if (Imp == null) {
				var flstr = string.Join("\", \"", Fields);
				if (Truncate) using (var trk = new NpgsqlCommand($"TRUNCATE TABLE {Table};", Conn)) trk.ExecuteNonQuery();
				if (Types is null) {
					using var cmd = new NpgsqlCommand($"SELECT \"{flstr}\" FROM {Table} WHERE 1=2;", Conn);
					using var rdr = cmd.ExecuteReader();
					Types = [.. rdr.GetColumnSchema().Select(x => x.NpgsqlDbType)];
				}
				Imp = Conn.BeginBinaryImport($"COPY {Table} (\"{flstr}\") FROM STDIN (FORMAT BINARY)");
			}
		}
		return Imp;
	}

	/// <summary>Įkelti įrašą</summary>
	/// <param name="row">duomenų įrašo laukai</param><param name="ct"></param>
	public async Task Insert(object?[] row, CancellationToken ct = default) { if (Types is null) await GetImp.WriteRowAsync(ct, row); else await Insert(row, Types, ct); RowCount++; }

	/// <summary>Įkelti įrašą</summary>
	/// <param name="row"></param><param name="types"></param><param name="ct"></param>
	public async Task Insert(object?[] row, NpgsqlDbType?[] types, CancellationToken ct = default) {
		await GetImp.StartRowAsync(ct);
		for (var i = 0; i < row.Length; i++) {
			var r = row[i]; var t = types[i];
			if (r is null) await GetImp.WriteNullAsync(ct);
			else if (t is null) await GetImp.WriteAsync(r, ct);
			else await GetImp.WriteAsync(r, t.Value, ct);
			RowCount++;
		}
	}

	/// <summary>Įkelti įrašą</summary>
	/// <param name="row"></param><param name="ct"></param>
	public async Task Insert(Dictionary<string, object?> row, CancellationToken ct = default) {
		var arr = new List<object?>();
		foreach (var i in Fields) { row.TryGetValue(i, out var val); arr.Add(val); }
		await Insert(arr.ToArray(), ct);
	}

	/// <summary>Užbaigti kėlimą</summary>
	/// <param name="ct"></param><returns></returns>
	public async Task Complete(CancellationToken ct = default) { var imp = GetImp; await imp.CompleteAsync(ct); await imp.DisposeAsync(); await Conn.DisposeAsync(); }


	/// <summary>Inicijuoti įkėlimą</summary>
	/// <param name="table">Lentelės pavadinimas</param>
	/// <param name="fld">Importuojami laukai</param>
	/// <param name="conn">DB pridijungimas</param>
	public DBImport(string table, List<string> fld, string conn) { Table = table; Fields = fld; Conn = new NpgsqlConnection(conn); Conn.Open(); }
	
	/// <summary>Inicijuoti įkėlimą</summary>
	/// <param name="table">Lentelės pavadinimas</param>
	/// <param name="fld">Importuojami laukai</param>
	/// <param name="conn">DB pridijungimas</param>
	public DBImport(string table, List<string> fld, NpgsqlConnection conn) { Table = table; Fields = fld; Conn = conn; if (conn.State != System.Data.ConnectionState.Open) conn.Open(); }
	
	/// <summary>Inicijuoti įkėlimą</summary>
	/// <param name="table">Lentelės pavadinimas</param>
	/// <param name="fld">Importuojami laukai</param>
	/// <param name="conn">DB pridijungimas</param>
	public DBImport(string table, List<string> fld, NpgsqlDataSource conn) { Table = table; Fields = fld; Conn = conn.OpenConnection(); }

	private bool IsDisposed;
	/// <summary>Atšaukti operaciją</summary>
	public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
	/// <summary>Atšaukti operaciją</summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing) {
		if (!IsDisposed) {
			if (disposing) { Imp?.Dispose(); Conn?.Dispose(); }
			IsDisposed = true;
		}
	}
}



