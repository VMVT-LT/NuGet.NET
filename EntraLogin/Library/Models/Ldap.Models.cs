using System.Text.RegularExpressions;

namespace Vmvt.EntraLogin.Models;

/// <summary>DN nuskaitymo modelis</summary>
public partial class LdapDN {
	/// <summary>DN tekstas</summary>
	public string? DN { get; set; }
	/// <summary>Pavadinimas</summary>
	public string? Name { get; set; }
	/// <summary>Kelias</summary>
	public string? Path { get; set; }
	[GeneratedRegex(@"^CN=(?<cn>[^,]+)", RegexOptions.IgnoreCase, "en-US")] private static partial Regex RgxOU();
	/// <summary>DN inicijavimas</summary>
	/// <param name="dn">DN tekstas</param>
	public LdapDN(string dn) {
		var cnMatch = RgxOU().Match(dn); Name = cnMatch.Success ? cnMatch.Groups["cn"].Value : null;
		Path = "/" + string.Join("/", dn.Split(',').Where(x => x.StartsWith("OU=", StringComparison.OrdinalIgnoreCase)).Select(x => x[3..]).Reverse());
	}
	/// <summary></summary>
	public LdapDN() { }
}

/// <summary>Bazinis Ldap vartotojo modelis</summary>
public class LdapUserBase {
	/// <summary>Prisijungimo vardas</summary>
	[LdapProp("userPrincipalName")] public string? Login { get; set; }
	/// <summary>Pilnas vardas</summary>
	[LdapProp("displayName")] public string? DisplayName { get; set; }
	/// <summary>Pareigos</summary>
	[LdapProp("title")] public string? JobTitle { get; set; }
	/// <summary>Organizacija</summary>
	[LdapProp("department")] public string? Department { get; set; }
}

/// <summary>Ldap vartotojo detalės</summary>
public class LdapUser : LdapUserBase {
	/// <summary>Pavardė</summary>
	[LdapProp("sn")] public string? LastName { get; set; }
	/// <summary>Vardas</summary>
	[LdapProp("givenName")] public string? FirstName { get; set; }
	/// <summary>El paštas (papildomas)</summary>
	[LdapProp("mail")] public string? Email { get; set; }
	/// <summary>Darbo adresas</summary>
	[LdapProp("physicalDeliveryOfficeName")] public string? Office { get; set; }
	/// <summary>Telefono numeris</summary>
	[LdapProp("telephoneNumber")] public string? Phone { get; set; }
	/// <summary>Mobilaus telefono numeris</summary>
	[LdapProp("mobile")] public string? Mobile { get; set; }
	/// <summary>AD prisijungimo vardas</summary>
	[LdapProp("sAMAccountName")] public string? LoginAD { get; set; }
	/// <summary>Tiesioginis vadovas</summary>
	[LdapProp("manager")] public LdapUserBase? Manager { get; set; }
	/// <summary>Grupės</summary>
	[LdapProp("memberOf")] public List<LdapDN>? Groups { get; set; }
}

/// <summary>Ldap klaidos atsakas</summary>
public class LdapUserError : LdapUser {
	/// <summary>Klaidos kodas</summary>
	public int Code { get; set; }
	/// <summary>Klaidos žnutė</summary>
	public string? Message { get; set; }
	/// <summary>Klaidos duomenys</summary>
	public string? ErrorData { get; set; }
}