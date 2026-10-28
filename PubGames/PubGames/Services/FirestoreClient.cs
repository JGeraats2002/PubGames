using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PubGames.Services;

/// <summary>
/// Minimal Cloud Firestore REST client, authenticated as the signed-in user
/// so firestore.rules decide what each call may read or write. Documents are
/// plain field dictionaries; see ToValue/FromValue for the supported types.
/// </summary>
public class FirestoreClient
{
	private const string BaseUrl =
		$"https://firestore.googleapis.com/v1/projects/{AuthConfig.FirebaseProjectId}/databases/(default)/documents";

	private readonly IAuthService _auth;
	private readonly HttpClient _http = new();

	/// <summary>
	/// Gets the services it works with - the sign-in - from the app (dependency injection).
	/// </summary>
	public FirestoreClient(IAuthService auth)
	{
		_auth = auth;
	}

	/// <summary>Creates or fully replaces collection/id.</summary>
	public async Task SetAsync(string collection, string id, IDictionary<string, object?> fields)
	{
		var body = new JsonObject { ["fields"] = ToFields(fields) };
		using var request = await CreateRequestAsync(HttpMethod.Patch, $"{BaseUrl}/{collection}/{Uri.EscapeDataString(id)}");
		request.Content = JsonContent.Create(body);
		await SendAsync(request);
	}

	/// <summary>
	/// Changes only these fields of an existing document, leaving the rest as
	/// it is (fails if the document doesn't exist).
	/// </summary>
	public async Task UpdateFieldsAsync(string collection, string id, IDictionary<string, object?> fields)
	{
		var mask = string.Join("&", fields.Keys.Select(k => $"updateMask.fieldPaths={Uri.EscapeDataString(k)}"));
		var body = new JsonObject { ["fields"] = ToFields(fields) };
		using var request = await CreateRequestAsync(HttpMethod.Patch,
			$"{BaseUrl}/{collection}/{Uri.EscapeDataString(id)}?{mask}&currentDocument.exists=true");
		request.Content = JsonContent.Create(body);
		await SendAsync(request);
	}

	/// <summary>Deletes collection/id. Deleting a document that doesn't exist is not an error.</summary>
	public async Task DeleteAsync(string collection, string id)
	{
		using var request = await CreateRequestAsync(HttpMethod.Delete, $"{BaseUrl}/{collection}/{Uri.EscapeDataString(id)}");
		await SendAsync(request);
	}

	/// <summary>One document's fields, or null if it doesn't exist.</summary>
	public async Task<Dictionary<string, object?>?> GetAsync(string collection, string id, CancellationToken ct = default)
	{
		using var request = await CreateRequestAsync(HttpMethod.Get, $"{BaseUrl}/{collection}/{Uri.EscapeDataString(id)}");
		var response = await _http.SendAsync(request, ct);
		if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
		if (!response.IsSuccessStatusCode)
			throw new CloudException((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

		var fields = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))?["fields"]?.AsObject();
		return fields?.ToDictionary(kv => kv.Key, kv => FromValue(kv.Value!));
	}

	/// <summary>All documents in a collection where field == value.</summary>
	public Task<List<Dictionary<string, object?>>> WhereEqualAsync(string collection, string field, object value) =>
		QueryAsync(collection, new JsonObject
		{
			["fieldFilter"] = new JsonObject
			{
				["field"] = new JsonObject { ["fieldPath"] = field },
				["op"] = "EQUAL",
				["value"] = ToValue(value)
			}
		});

	/// <summary>All documents in a collection whose array field contains value.</summary>
	public Task<List<Dictionary<string, object?>>> WhereArrayContainsAsync(string collection, string field, object value) =>
		QueryAsync(collection, new JsonObject
		{
			["fieldFilter"] = new JsonObject
			{
				["field"] = new JsonObject { ["fieldPath"] = field },
				["op"] = "ARRAY_CONTAINS",
				["value"] = ToValue(value)
			}
		});

	/// <summary>Every document in a collection. The rules must allow reading all of them, or the whole query is refused.</summary>
	public Task<List<Dictionary<string, object?>>> GetAllAsync(string collection) => QueryAsync(collection, where: null);

	/// <summary>
	/// Runs a query on a collection (all documents, or those matching `where`) and returns their fields.
	/// </summary>
	private async Task<List<Dictionary<string, object?>>> QueryAsync(string collection, JsonObject? where)
	{
		var structuredQuery = new JsonObject
		{
			["from"] = new JsonArray(new JsonObject { ["collectionId"] = collection })
		};
		if (where is not null)
			structuredQuery["where"] = where;
		var query = new JsonObject { ["structuredQuery"] = structuredQuery };

		using var request = await CreateRequestAsync(HttpMethod.Post, $"{BaseUrl}:runQuery");
		request.Content = JsonContent.Create(query);
		var response = await SendAsync(request);

		// runQuery streams one entry per match; entries without "document" are progress markers.
		var results = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
		return results
			.Select(r => r?["document"]?["fields"]?.AsObject())
			.Where(f => f is not null)
			.Select(f => f!.ToDictionary(kv => kv.Key, kv => FromValue(kv.Value!)))
			.ToList();
	}

	/// <summary>
	/// A request to the cloud signed with the user's ID token, so the security rules know who's asking.
	/// </summary>
	private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
	{
		var token = await _auth.GetIdTokenAsync()
			?? throw new InvalidOperationException(L.T("Sign in to sync with the cloud."));
		var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return request;
	}

	/// <summary>Sends a request; a refusal becomes a CloudException with a readable message.</summary>
	private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
	{
		var response = await _http.SendAsync(request);
		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			throw new CloudException((int)response.StatusCode, error);
		}
		return response;
	}

	/// <summary>Plain values as Firestore's typed fields.</summary>
	private static JsonObject ToFields(IDictionary<string, object?> fields)
	{
		var result = new JsonObject();
		foreach (var (key, value) in fields)
			result[key] = ToValue(value);
		return result;
	}

	/// <summary>
	/// One plain value (text, number, yes/no, date, list, map, bytes, nothing) as a Firestore typed value.
	/// </summary>
	private static JsonObject ToValue(object? value) => value switch
	{
		null => new JsonObject { ["nullValue"] = null },
		string s => new JsonObject { ["stringValue"] = s },
		bool b => new JsonObject { ["booleanValue"] = b },
		int or long => new JsonObject { ["integerValue"] = Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture) },
		double or decimal or float => new JsonObject { ["doubleValue"] = Convert.ToDouble(value) },
		byte[] bytes => new JsonObject { ["bytesValue"] = Convert.ToBase64String(bytes) },
		DateTime d => new JsonObject { ["timestampValue"] = DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) },
		IDictionary<string, object?> map => new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = ToFields(map) } },
		IEnumerable<string> list => new JsonObject
		{
			["arrayValue"] = new JsonObject { ["values"] = new JsonArray(list.Select(item => (JsonNode)ToValue(item)).ToArray()) }
		},
		_ => throw new ArgumentException(L.F("Unsupported Firestore field type {0}", value.GetType().Name))
	};

	/// <summary>An array field of strings (e.g. a game's category ids); empty when missing.</summary>
	public static List<string> AsStringList(object? value) =>
		value is List<object?> items ? items.OfType<string>().ToList() : [];

	/// <summary>One Firestore typed value back as a plain value.</summary>
	private static object? FromValue(JsonNode value)
	{
		var obj = value.AsObject();
		if (obj.TryGetPropertyValue("stringValue", out var s)) return s!.GetValue<string>();
		if (obj.TryGetPropertyValue("booleanValue", out var b)) return b!.GetValue<bool>();
		if (obj.TryGetPropertyValue("integerValue", out var i)) return long.Parse(i!.GetValue<string>(), CultureInfo.InvariantCulture);
		if (obj.TryGetPropertyValue("doubleValue", out var d)) return d!.GetValue<double>();
		if (obj.TryGetPropertyValue("bytesValue", out var by)) return Convert.FromBase64String(by!.GetValue<string>());
		if (obj.TryGetPropertyValue("timestampValue", out var t))
			return DateTime.Parse(t!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
		if (obj.TryGetPropertyValue("arrayValue", out var a))
			// An empty array comes back without "values".
			return a?["values"]?.AsArray().Select(v => FromValue(v!)).ToList() ?? new List<object?>();
		if (obj.TryGetPropertyValue("mapValue", out var m))
			// An empty map comes back without "fields".
			return m?["fields"]?.AsObject().ToDictionary(field => field.Key, field => FromValue(field.Value!)) ?? new Dictionary<string, object?>();
		return null;
	}
}

/// <summary>
/// The cloud refused a request, with a message the user can understand (no permission, not found, or the
/// cloud's own message).
/// </summary>
public class CloudException(int statusCode, string details)
	: Exception(statusCode == 403
		? L.T("The cloud refused this change - you don't have permission for it. Ask an admin to check your rights on the Team page.")
		: L.F("Cloud request failed ({0}): {1}", statusCode, details))
{
	/// <summary>The HTTP status the cloud answered with (403: no permission, 404: not found ...).</summary>
	public int StatusCode { get; } = statusCode;
}
