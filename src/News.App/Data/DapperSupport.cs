namespace News.App.Data;

using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;

static class DapperSupport
{
    public static async Task<T?> QueryJsonAsync<T>(this DbConnection cnn, string sql, object? param = null)
    {
        await using var reader = await cnn.ExecuteReaderAsync(sql, param);
        var json = new StringBuilder(1024 * 3);
        while (await reader.ReadAsync())
        {
            json.Append(reader.GetString(0));
        }

        if (json.Length == 0)
            return default;

        return JsonSerializer.Deserialize<T>(json.ToString());
    }
}
