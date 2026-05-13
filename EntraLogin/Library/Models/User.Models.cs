namespace Vmvt.EntraLogin.Models;


/// <summary>Vartotojo bazinė informacija</summary>
public class UserBase {
	/// <summary>Prisijungimo vardas (UPN/Email)</summary>
	public string? Login { get; set; }
	/// <summary>Pilnas vardas</summary>
	public string? DisplayName { get; set; }
	/// <summary>Pareigos</summary>
	public string? JobTitle { get; set; }
	/// <summary>Padalinys</summary>
	public string? Department { get; set; }
}


/// <summary>Vartotojo informacija</summary>
public class UserDetails : UserBase {
	/// <summary>Sisteminis vartotojo id</summary>
	public Guid? ID { get; set; }
	/// <summary>AD prisijungimo vardas</summary>
	public string? LoginAD { get; set; }
	/// <summary>Vardas</summary>
	public string? FirstName { get; set; }
	/// <summary>Pavardė</summary>
	public string? LastName { get; set; }
	/// <summary>Darbo adresas</summary>
	public string? Office { get; set; }
	/// <summary>El.Paštas</summary>
	public string? Email { get; set; }
	/// <summary>Telefono numeris</summary>
	public string? Phone { get; set; }
	/// <summary>Mobilaus telefono numeris</summary>
	public string? Mobile { get; set; } 
}
