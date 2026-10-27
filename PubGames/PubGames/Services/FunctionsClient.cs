using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PubGames.Services;

/// <summary>
/// Calls the app's server functions (functions/index.js) as the signed-in
/// user: the server reads who's asking from the sign-in token, so a user can
/// only act for themselves. Used for purchases, the friend code and deleting
/// an account.
/// </summary>
public class FunctionsClient(IAuthService auth)
{
	private const string FunctionsUrl = $"https://europe-west1-{AuthConfig.FirebaseProjectId}.cloudfunctions.net";

	private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

	/// <summary>
	/// Calls one function (a Firebase "callable") and returns its result.
	/// Throws StoreException with the server's message when it refuses, and
	/// HttpRequestException when there's no connection.
	/// </summary>
	public async Task<JsonNode?> CallAsync(string function, JsonObject data)
	{
		var token = await auth.GetIdTokenAsync() ?? throw new StoreException("Sign in first.");
		HttpResponseMessage? response = null;
		// Very busy (every server spot taken): wait a moment and try once more.
		for (var attempt = 0; attempt < 2; attempt++)
		{
			response?.Dispose();
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{FunctionsUrl}/{function}")
			{
				Content = JsonContent.Create(new JsonObject { ["data"] = data.DeepClone() })
			};
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			response = await _http.SendAsync(request);
			if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable)) break;
			await Task.Delay(TimeSpan.FromSeconds(2));
		}
		using var finished = response!;
		var body = JsonNode.Parse(await finished.Content.ReadAsStringAsync());
		if (body?["error"] is { } error)
			throw new StoreException(error["message"]?.GetValue<string>() ?? "The server refused this.");
		if (!finished.IsSuccessStatusCode)
			throw new StoreException("The server couldn't be reached right now. Try again later.");
		return body?["result"];
	}
}
