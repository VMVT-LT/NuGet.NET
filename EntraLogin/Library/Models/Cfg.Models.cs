namespace Vmvt.EntraLogin.Models;

/// <summary>EntraID prisijungimo konfiguracija</summary>
public class EntraCfg {
	/// <summary>EntraID OAuth2 bazinė konfigūracija</summary>
	public EntraCfgAuth Auth { get; set; } = new();
	/// <summary>Vartotojo sesijos konfigūracija</summary>
	public EntraCfgSession Session { get; set; } = new();
	/// <summary>Prieigos taškų konfigūracija</summary>
	public EntraCfgEndpoints Endpoints { get; set; } = new();
	/// <summary>Graph užklausos</summary>
	public EntraCfgGraph Graph { get; set; } = new();
	/// <summary>Pakartotinių prisijungimų apsauga (DDOS)</summary>
	public EntraCfgLock Lock { get; set; } = new();
	/// <summary>Ldap prisijungimo detalės</summary>
	public EntraCfgLdap Ldap { get; set; } = new();

	/// <summary>Klaidų apdorojimas</summary>
	public Func<EntraException, Task>? ErrorHandler { get; set; }
	/// <summary>Klaidų apdorojimas</summary>
	public Func<SessionLogType, UserSession, Task>? SessionHandler { get; set; }
	/// <summary>Klaidų apdorojimas</summary>
	public Func<string, Task<UserSession?>>? RestoreHandler { get; set; }


	/// <summary>Sesijos duomenų tipas</summary>
	public Type? SessionData { get; set; }
	/// <summary>Vartotojo grupių konfigūracija</summary>
	public List<GroupMap>? GroupList { get; set; }

	/// <summary>Detalesnis klaidų identifikavimas</summary>
	public bool Debug { get; set; }
}

/// <summary>Sesijos parametrai</summary>
public class EntraCfgSession {
	/// <summary>Slapuko pavadinimas</summary>
	public string CookieName { get; set; } = "SSID";
	/// <summary>Sesijos autorizacijos rakto ilgis</summary>
	public int KeyLength { get; set; } = 64;
	/// <summary>Sesijos galiojimo laikas sekundėmis</summary>
	public long Expire { get; set; } = 1800;
	/// <summary>Sesijos atnaujinimo laikas sekundėmis</summary>
	public long Extend { get; set; } = 300;
	/// <summary>Pakartotinio prisijungimo (prompt) reikalavimo laikas sekundėmis</summary>
	public long Keep { get; set; } = 86400;
	/// <summary>Pasibaigusių sesijų pašalinimo tikrinimo intervalas sekundėmis</summary>
	public int CleanExpired { get; set; } = 300;
	/// <summary>Prisijungimo klaidų saugojimo laikas sekundėmis</summary>
	public int CleanErrors { get; set; } = 600;
	/// <summary>Tikrinti ar nepasikeitė sesijai skirtas UserAgent</summary>
	public bool StrictUA { get; set; } = true;
	/// <summary>Tikrinti ar nepasikeitė sesijai skirtas IP</summary>
	public bool StrictIP { get; set; } = false;
}

/// <summary>Autorizacijos parametrai (OAuth2)</summary>
public class EntraCfgAuth {
	/// <summary>Aplinkos identifikatorius (Tenant GUID arba „common“)</summary>
	public string Tenant { get; set; } = "";
	/// <summary>Programos kliento identifikatorius</summary>
	public string ClientId { get; set; } = "";
	/// <summary>Programos kliento raktas</summary>
	public string ClientSecret { get; set; } = "";
	/// <summary>Adreso vardas</summary>
	public string Host { get; set; } = "login.microsoftonline.com";
	/// <summary>Autorizacijos nukreipimas</summary>
	public string UrlAuth { get; set; } = "/oauth2/authorize";
	/// <summary>Prieigos rakto gavimas</summary>
	public string UrlToken { get; set; } = "/oauth2/v2.0/token";
	/// <summary>Sritis / Prieigos lygis</summary>
	public string Scope { get; set; } = "User.Read User.Read.All";
	/// <summary>Autorizacijos atsako laukimas sekundėmis</summary>
	public int Timeout { get; set; } = 300;
}

/// <summary>Graph duomenų užklausos</summary>
public class EntraCfgGraph {
	/// <summary>Adresas</summary>
	public string Host { get; set; } = "graph.microsoft.com";
	/// <summary>Versija</summary>
	public string Version { get; set; } = "v1.0";
	/// <summary>Vartotojo duomenų gavimo adresas</summary>
 	public string GetUser { get; set; } = "/me?$select=id,userPrincipalName,displayName,givenName,surname,jobTitle,department,mail,mobilePhone,businessPhones,onPremisesSamAccountName,officeLocation&$expand=manager($select=userPrincipalName,displayName,department,jobTitle)";
	/// <summary>Vartotojų grupių gavimo adresas</summary>
	public string GetGroups { get; set; } = "/me/memberOf?$top=100&$select=id,displayName";
}

/// <summary>Prieigos taškų konfigūracija</summary>
public class EntraCfgEndpoints {
	/// <summary>Pagrindinis svetainės adresas (URL)</summary>
	public string Host { get; set; } = "http://localhost:5502";
	/// <summary>Numatytasis nukreipimo adresas po prisijungimo</summary>
	public string Base { get; set; } = "/";
	/// <summary>Autorizacijos atsako grąžinimo adresas</summary>
	public string Callback { get; set; } = "/auth/login";
	/// <summary>Numatytasis nukreipimo adresas po prisijungimo</summary>
	public string Return { get; set; } = "/";
	/// <summary>Klaidos nukreipimo adresas</summary>
	public string Error { get; set; } = "/klaida?err=";
	/// <summary>Nukreipimas po atsijungimo</summary>
	public string PostLogout { get; set; } = "/auth/login?p=select";
	/// <summary>Ldap prisijungimo UI</summary>
	public string? LdapForm { get; set; }
	/// <summary>Ldap prisijungimo adresas</summary>
	public string? LdapPost { get; set; }
}

/// <summary>Pakartotinių prisijungimų apsauga (DDOS)</summary>
public class EntraCfgLock {
	/// <summary>Pridedamos vėlinimas sekundės po kiekvieno bandymo iš to paties IP</summary>
	public int Delay { get; set; } = 1;
	/// <summary>IP duomenų išvalymo intervalas sekundėmis</summary>
	public int CleanInterval { get; set; } = 120;
	/// <summary>IP duomenų išvalymo delsa sekundėmis</summary>
	public int CleanDelay { get; set; } = 300;
	/// <summary>Pakartotinių bandymų skaičius iš vieno IP adreso iki pranešimo</summary>
	public int Report { get; set; } = 10;
}

/// <summary>Ldap prisijungimo konfigūracija</summary>
public class EntraCfgLdap {
	/// <summary>Leisti Ldap prisijungimą</summary>
	public bool AllowLdap { get; set; }
	/// <summary>Naudoti kaip pagrindinį prisijungimą</summary>
	public bool ForceLdap { get; set; }
	/// <summary>Hostname</summary>
	public string Host { get; set; } = "domain.com";
	/// <summary>Port</summary>
	public int Port { get; set; } = 636;
	/// <summary>Paieškos vieta</summary>
	public string BaseDN { get; set; } = "OU=Users,dc=domain,dc=com";
	/// <summary>Prisijungimo paieška</summary>
	public string Search { get; set; } = "userPrincipalName";
	/// <summary>Vartotojo atributai</summary>
	public string[] UserSelect { get; set; } = ["sn", "givenName", "title", "physicalDeliveryOfficeName", "telephoneNumber", "mobile", "displayName", "department", "streetAddress", "sAMAccountName", "userPrincipalName", "mail", "memberOf", "manager"];
	/// <summary>Standartiniai vartotojo atributai</summary>
	public string[] UserDefault { get; set; } = ["userPrincipalName", "displayName", "title", "department"];
}
