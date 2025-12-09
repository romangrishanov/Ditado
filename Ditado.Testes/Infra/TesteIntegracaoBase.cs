using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ditado.Aplicacao.DTOs.Usuarios;
using Xunit;

namespace Ditado.Testes.Infra;

public abstract class TesteIntegracaoBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly HttpClient Client;
    private string? _tokenAdmin; // Cache do token admin
    
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected TesteIntegracaoBase(CustomWebApplicationFactory factory)
    {
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Obtém token do admin temporário (criado no seed da factory)
    /// </summary>
    protected async Task<string> ObterTokenAdminAsync()
    {
        if (_tokenAdmin != null)
            return _tokenAdmin;

        var loginRequest = new LoginRequest
        {
            Login = "admin@admin.com",
            Senha = "admin"
        };

        var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", loginRequest);

        if (loginResponse?.Token == null)
            throw new InvalidOperationException("Não foi possível obter token de admin. Certifique-se de que o usuário admin@admin.com existe.");

        _tokenAdmin = loginResponse.Token;
        return _tokenAdmin;
    }

    protected async Task<TResponse?> PostAsync<TResponse>(string url, object data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
            return default;

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions);
    }

    protected async Task<TResponse?> PutAsync<TResponse>(string url, object data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PutAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
            return default;

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions);
    }

    protected async Task<TResponse?> GetAsync<TResponse>(string url)
    {
        var response = await Client.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
            return default;

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions);
    }

    protected async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        return await Client.DeleteAsync(url);
    }

    protected async Task<HttpResponseMessage> PostAsyncRaw(string url, object data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );

        return await Client.PostAsync(url, content);
    }

    protected async Task<HttpResponseMessage> GetAsyncRaw(string url)
    {
        return await Client.GetAsync(url);
    }

    protected void AdicionarTokenAutorizacao(string token)
    {
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    protected void RemoverTokenAutorizacao()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }
}