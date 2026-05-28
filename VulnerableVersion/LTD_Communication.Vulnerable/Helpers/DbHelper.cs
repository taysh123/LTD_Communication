using Microsoft.Data.SqlClient;

namespace LTD_Communication.Vulnerable.Helpers;

// VULNERABLE: All methods use raw string concatenation.
// No parameterized queries — susceptible to SQL Injection attacks.
public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// VULNERABLE: Executes a raw SQL string with no parameterization.
    /// An attacker can inject arbitrary SQL via user-supplied input.
    /// </summary>
    public List<Dictionary<string, object?>> ExecuteQuery(string sql)
    {
        var results = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    /// <summary>
    /// VULNERABLE: Executes a raw SQL string — no parameterization.
    /// </summary>
    public int ExecuteNonQuery(string sql)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// VULNERABLE: Returns scalar result from a raw SQL string.
    /// </summary>
    public object? ExecuteScalar(string sql)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }
}
