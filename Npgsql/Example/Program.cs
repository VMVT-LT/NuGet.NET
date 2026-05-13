using Example;
using System.Text.Json.Serialization;
using Vmvt.Npgsql;

// Priskiriamas standartinis DB prisijungimas
//    galima sukurti new DB("connstring"); ir naudoti metoduose.
//    Debug - spausdinti užklausas konsolėje
DB.Default = new("User ID=postgres; Password=postgres; Server=localhost:5432; Database=postgres;") { Debug=true };


//Duombazės paruošimas
using var dbr = new DBRead("CREATE SCHEMA IF NOT EXISTS example; CREATE TABLE IF NOT EXISTS example.table (id INT PRIMARY KEY, grp TEXT, name TEXT);");
await dbr.Execute();

// Užklausos vykdymas
using var dbr1 = new DBRead("INSERT INTO example.table (id, grp, name) VALUES (@id,@group,@name);", ("@id", 1), ("@group", "Group"), ("@name", "Name"));
var cnt = await dbr1.Execute();

// Gauti pirmą įrašą
using var dbr2 = new DBRead("SELECT * FROM example.table WHERE id=1;");
var obj = await dbr2.GetObject<TestObject>();

// Gauti sąrašą
using var dbr3 = new DBRead("SELECT * FROM example.table WHERE grp=@grp;", ("@grp", "Filter"));
var lst = await dbr3.GetList<TestObject>();

// Gauti reikšmę
using var dbr4 = new DBRead("SELECT version();");
var ret = await dbr4.GetScalar<string>();

// Gauti JSONB objektą
using var dbr5 = new DBRead("SELECT jsonb_build_object('id',1,'grp','Grupė','name','Vardas');");
var jso = await dbr5.GetJsonbObject<TestObject>();


namespace Example {
	// [DbField()] - naudojamas nurodyti nestandartinį DB stulpelio pavadinimą
	public class TestObject {
		public int Id { get; set; }
		public string? Group { get; set; }
		public string? Name { get; set; }
		[DbField("string_array")] public List<string>? List { get; set; }
		[DbField("jsonb_data")] public TestData? Data { get; set; }
	}
	// Jsonb nuskaitomas naudojant System.Text.Json
	public class TestData {
		[JsonPropertyName("num")] public int Number { get; set; }
		[JsonPropertyName("txt")] public string? Text { get; set; }
	}

}
