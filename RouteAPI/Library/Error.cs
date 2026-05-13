using Microsoft.AspNetCore.Http;

namespace Vmvt.RouteAPI;


/// <summary>Klaidos standartinis modelis</summary>
public class ErrorResponse {
	/// <summary>Klaidos nuoroda</summary>
	public Guid Ref { get; set; } = Guid.NewGuid();
	/// <summary>Klaidos kodas</summary>
	/// <example>200</example>
	public virtual int Code { get; set; }
	/// <summary>Klaidos statusas</summary>
	/// <example>Error status</example>
	public virtual string Status { get; set; }
	/// <summary>Klaidos aprašymas</summary>
	/// <example>Klaidos žinutė</example>
	public virtual string Message { get; set; }
	/// <summary>Klaidos informacija</summary>
	/// <example>["Papildoma klaidos informacija"]</example>
	public List<string>? Details { get; set; }

	/// <summary></summary>
	public ErrorResponse() { Message = Status = string.Empty; }

	/// <summary></summary>
	/// <param name="code"></param>
	/// <param name="msg"></param>
	/// <param name="details"></param>
	public ErrorResponse(int code, string msg, params string[] details) {
		Code = code; Message = msg; Details = details.Length > 0 ? [.. details] : null;
		Status = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(code);
	}
}

/// <summary></summary>
public static class ErrorExtensions {

	/// <summary></summary><param name="rsp"></param>
	/// <param name="err"></param><returns></returns>
	public static async Task<ErrorResponse> Error(this HttpResponse rsp, ErrorResponse err) {
		if (!rsp.HasStarted) {
			rsp.StatusCode = err.Code;
			await rsp.WriteAsJsonAsync(err);
		}
		return err;
	}

	/// <summary></summary><param name="rsp"></param><param name="msg"></param><returns></returns>
	public static async Task<ErrorResponse> Error(this HttpResponse rsp, string msg = "") => await rsp.Error(new ErrorResponse(rsp.StatusCode, msg));

	/// <summary></summary><param name="rsp"></param><param name="status"></param><param name="msg"></param><param name="str"></param><returns></returns>
	public static async Task<ErrorResponse> Error(this HttpResponse rsp, int status, string msg, params string[] str) => await rsp.Error(new ErrorResponse(status, msg, str));

	/// <summary>Užklausos klaida</summary>
	public static async Task<ErrorResponse> E400(this HttpResponse rsp, string? msg =null, params string[] str) => await rsp.Error(400, msg??"Užklausos klaida", str);
	/// <summary>Kritinė klaida</summary>
	public static async Task<ErrorResponse> E500(this HttpResponse rsp, string? msg = null, params string[] str) => await rsp.Error(500, msg??"Sistemos klaida", str);
	/// <summary>Nerasta</summary>
	public static async Task<ErrorResponse> E404(this HttpResponse rsp, string msg, params string[] str) => await rsp.Error(404, msg, str);


	/// <summary>Autorizacijos klaida</summary>
	public static async Task<ErrorResponse> E401(this HttpResponse rsp) => await rsp.Error(Er401);
	private static ErrorResponse Er401 { get; } = new() { Code = 401, Status = "Unauthorized", Message = "Reikalinga vartotojo autorizacija" };
	
	/// <summary>Prieigos klaida</summary>
	public static async Task<ErrorResponse> E403(this HttpResponse rsp) => await rsp.Error(Er403);
	private static ErrorResponse Er403 { get; } = new() { Code = 403, Status = "Forbidden", Message = "Jūs neturite prieigos prie šio resourso" };

	/// <summary>Nerasto resurso klaida</summary>
	public static async Task<ErrorResponse> E404(this HttpResponse rsp) => await rsp.Error(Er404);
	private static ErrorResponse Er404 { get; } = new() { Code = 404, Status = "Not Found", Message = "Resursas kurio ieškote neegzistuoja" };

}
