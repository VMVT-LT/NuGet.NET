using System.Text.Json.Serialization;

namespace Vmvt.EntraLogin.Models;


/// <summary>Autorizacijos apsauga</summary>
public class AuthLock {
	/// <summary>Pradinis autorizacijos laikas</summary>
	public DateTime Start { get; set; } = DateTime.UtcNow;
	/// <summary>Paskutinės Autorizacijos laikas</summary>
	public DateTime LastLock { get; set; } = DateTime.UtcNow;
	/// <summary>Autorizacijos kiekis</summary>
	public long Count { get; set; } = 0;
}

/// <summary>Autorizacijos užklausa</summary>
public class AuthRequest {
	/// <summary>Autorizacijos identifikavimo numeris</summary>
	public string State { get; } = Extensions.RandomStr(12);
	/// <summary>Autorizacijos identifikavimo numeris</summary>
	public Guid ID { get; } = Guid.NewGuid();
	/// <summary>Vartotojo IP adresas</summary>
	public string? IP { get; set; }
	/// <summary>Vartotojo peradresavimas po autorizacijos</summary>
	public string Return { get; set; } = "/";
	/// <summary>Vartotojo autorizacijos laiko limitas</summary>
	public DateTime? Timeout { get; set; }
	/// <summary>Vartotojui buvo prisijungimo paklausimas</summary>
	public bool Prompt { get; set; }
}

/// <summary>Autorizacijos formos modelis</summary>
public class AuthForm {
	/// <summary>Prisijungimo iniciavimo kodas</summary>
	public string? State { get; set; }
	/// <summary>Atsako kodas</summary>
	public string? Code { get; set; }
	/// <summary>Entra užklausos kodas</summary>
	public Guid? Request { get; set; }
	/// <summary>Vartotojo vardas</summary>
	public string? User { get; set; }
	/// <summary>Vartotojo slaptažodis</summary>
	public string? Pass { get; set; }
	/// <summary>Ldap prisijungimas</summary>
	public bool Ldap { get; set; }
	/// <summary>Peradresuoti užklausą</summary>
	public AuthPrompt? Redirect { get; set; }
	/// <summary>EntraID klaidos kodas</summary>
	public string? ErrCode { get; set; }
	/// <summary>EntraID klaidos aprašymas</summary>
	public string? ErrDescr { get; set; }
}

/// <summary>EntraID Access Token</summary>
public class AccessToken {
	/// <summary>Prieigos raktas</summary>
	[JsonPropertyName("access_token")] public string? AT { get; set; }
	/// <summary>Galiojimo laikas</summary>
	[JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
	/// <summary>Pratęsimo laikas</summary>
	[JsonPropertyName("ext_expires_in")] public int ExtExpiresIn { get; set; }
	/// <summary>Sritis</summary>
	[JsonPropertyName("scope")] public string? Scope { get; set; }
	/// <summary>Tipas</summary>
	[JsonPropertyName("token_type")] public string? Type { get; set; }
}

/// <summary>Entra ID Access token response</summary>
public class AccessTokenResponse : AccessToken {
	/// <summary>Klaidos ID</summary>
	[JsonPropertyName("correlation_id")] public Guid? ErrorId { get; set; }
	/// <summary>Klaida</summary>
	[JsonPropertyName("error")] public string? Error { get; set; }
	/// <summary>Klaidos aprašymas</summary>
	[JsonPropertyName("error_description")] public string? ErrorDescr { get; set; }
	/// <summary>Klaidos adresas</summary>
	[JsonPropertyName("error_uri")] public string? ErrorUrl { get; set; }
	/// <summary>Klaidos atsekimas</summary>
	[JsonPropertyName("trace_id")] public Guid? TraceId { get; set; }
}




/// <summary>Prisijungimo klaidos išimčių modelis</summary>
public class EntraException : Exception {
	/// <summary>Sąsajos numeris</summary>
	public Guid Ref { get; } = Guid.NewGuid();
	/// <summary>Klaidos kodas</summary>
	public int Code { get; set; }
	/// <summary>Klaidos aprašymas</summary>
	public string? Details { get; set; }
	/// <summary>Klaidos detalės, duomenys</summary>
	public object? DataObject { get; set; }
	/// <summary>Rekomendacija naudoti kitą autorizaciją</summary>
	public bool Fallback { get; set; }
	/// <summary>Klaidos pranešimo data</summary>
	public DateTime Date { get; set; } = DateTime.UtcNow;
	/// <summary>Vartotojo sesijos informacija</summary>
	public UserSession? Session { get; set; }

	/// <summary></summary>
	/// <param name="code">Klaidos kodas</param>
	/// <param name="source">Kalidos vieta</param>
	/// <param name="message">Trumpa žinutė</param>
	/// <param name="data">Duomenys</param>
	public EntraException(int code, string source, string message, object? data = null) : base(message) {
		Code = code; Source = source; DataObject = data;
	}
	/// <summary>Klaidos konvertavimas</summary>
	/// <returns>Supaprastintas klaidos modelis</returns>
	public EntraError ToError() => new() { Ref = Ref, Code = Code, Message = Message, Details = Details };
}


/// <summary>Suparastintas klaidos modelis</summary>
public class EntraError {
	/// <summary>Klaidos sąsajos numeris</summary>
	public Guid? Ref { get; set; }
	/// <summary>Klaidos kodas</summary>
	public int? Code { get; set; }
	/// <summary>Trumpas klaidos aprašymas</summary>
	public string? Message { get; set; }
	/// <summary>Klaidos detalės</summary>
	public string? Details { get; set; }
}


/// <summary>Prisijungimo reikalavimas</summary>
public enum AuthPrompt {
	/// <summary>Numatytoji (tylioji)</summary>
	None,
	/// <summary>Leisti pasirinkti vartotoją</summary>
	Select,
	/// <summary>Priverstinai prisijungti</summary>
	Login,
	/// <summary>Patvirtinti prisijungimo teises</summary>
	Consent,
	/// <summary>Ldap prisijungimas</summary>
	Ldap
}


