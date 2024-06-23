using System.Text.Json;

namespace FantasyF1.Helpers;

public static class HttpHelper
{

    public static async Task<T> GetAsync<T>(string endpoint)
    {
        try
        {
            using var client = new HttpClient();
            var url = "https://api.openf1.org/v1/";
            client.BaseAddress = new Uri(url);

            var response = await client.GetAsync(endpoint);

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            var res = JsonSerializer.Deserialize<T>(responseBody);

            return res;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}