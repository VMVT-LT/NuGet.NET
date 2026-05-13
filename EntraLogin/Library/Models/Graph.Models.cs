using System.Text.Json.Serialization;

namespace Vmvt.EntraLogin.Models;

/// <summary>Bazinis Graph atsakas</summary>
public class GraphResponse {
	/// <summary>Užklausos adresas</summary>
	[JsonPropertyName("@odata.context")] public string? ContextUrl { get; set; }
	/// <summary>Klaida</summary>
	[JsonPropertyName("error")] public GraphError? Error { get; set; }
}

/// <summary>Graph sąrašo atsakas</summary>
/// <typeparam name="T">Sąrašo objekto klasė</typeparam>
public class GraphResponse<T> : GraphResponse {
	/// <summary>Kito puslapio adresas</summary>
	[JsonPropertyName("@odata.nextLink")] public string? NextUrl { get; set; }
	/// <summary>Reikšmės</summary>
	[JsonPropertyName("value")] public List<T>? Data { get; set; }
}

/// <summary>Klaidos pranešimas</summary>
public class GraphError {
	/// <summary>Klaidos kodas</summary>
	[JsonPropertyName("code")] public string? Code { get; set; }
	/// <summary>Klaidos kodas</summary>
	[JsonPropertyName("message")] public string? Message { get; set; }
	/// <summary>Klaidos kodas</summary>
	[JsonPropertyName("innerError")] public GraphErrorInner? Details { get; set; }
}

/// <summary>Vidinis klaidos pranešimas</summary>
public class GraphErrorInner {
	/// <summary></summary>
	[JsonPropertyName("client-request-id")] public Guid? ClientRequestId { get; set; }
	/// <summary></summary>
	[JsonPropertyName("date")] public DateTime? Date { get; set; }
	/// <summary></summary>
	[JsonPropertyName("request-id")] public Guid? RequestId { get; set; }
}

/// <summary>Grupės informacijos modelis</summary>
public class GraphGroup {
	/// <summary>Grupės ID</summary>
	[JsonPropertyName("id")] public Guid? Id { get; set; }
	/// <summary>Grupės pavadinimas</summary>
	[JsonPropertyName("displayName")] public string? Name { get; set; }
	/// <summary>Grupės aprašymas</summary>
	[JsonPropertyName("description")] public string? Description { get; set; }
}

/// <summary>Vartotojo bazinė informacija</summary>
public class GraphUserBase : GraphResponse {
	/// <summary>Prisijungimo vardas (UPN/Email)</summary>
	[JsonPropertyName("userPrincipalName")] public string? Login { get; set; }
	/// <summary>Pilnas vardas</summary>
	[JsonPropertyName("displayName")] public string? DisplayName { get; set; }
	/// <summary>Pareigos</summary>
	[JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }
	/// <summary>Padalinys</summary>
	[JsonPropertyName("department")] public string? Department { get; set; }
}

/// <summary>Vartotojo informacijos modelis</summary>
public class GraphUser : GraphUserBase {
	/// <summary>Vartotojo ID</summary>
	[JsonPropertyName("id")] public Guid? ID { get; set; }
	/// <summary>AD prisijungimo vardas</summary>
	[JsonPropertyName("onPremisesSamAccountName")] public string? LoginAD { get; set; }
	/// <summary>Vardas</summary>
	[JsonPropertyName("givenName")] public string? FirstName { get; set; }
	/// <summary>Pavardė</summary>
	[JsonPropertyName("surname")] public string? LastName { get; set; }
	/// <summary>Darbo adresas</summary>
	[JsonPropertyName("officeLocation")] public string? Office { get; set; }
	/// <summary>El paštas (papildomas)</summary>
	[JsonPropertyName("mail")] public string? Email { get; set; }
	/// <summary>Telefono numeris</summary>
	[JsonConverter(typeof(FirstConverter))]
	[JsonPropertyName("businessPhones")] public string? Phone { get; set; }
	/// <summary>Mobilaus telefono numeris</summary>
	[JsonPropertyName("mobilePhone")] public string? Mobile { get; set; }
	/// <summary>Tiesioginis vadovas</summary>
	[JsonPropertyName("manager")] public GraphUserBase? Manager { get; set; }
	/// <summary>Vartotojo grupės </summary>
	public List<GraphGroup> Groups { get; set; } = [];
}
