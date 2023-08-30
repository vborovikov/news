namespace News.App.Data;

using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;

static class DapperSupport
{
    public static async Task<string> QueryTextAsync(this DbConnection cnn, string sql, object? param = null)
    {
        await using var reader = await cnn.ExecuteReaderAsync(sql, param);
        var text = new StringBuilder(1024 * 3);
        while (await reader.ReadAsync())
        {
            text.Append(reader.GetString(0));
        }
        return text.ToString();
    }

    public static async Task<T?> QueryJsonAsync<T>(this DbConnection cnn, string sql, object? param = null)
    {
        var json = await cnn.QueryTextAsync(sql, param);
        if (json.Length == 0)
            return default;

        return JsonSerializer.Deserialize<T>(json);
    }
}
