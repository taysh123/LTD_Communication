using Microsoft.Data.SqlClient;

namespace LTD_Communication.Secure.Helpers;

// SECURE: All methods require explicit SqlParameter arrays.
// No string interpolation or concatenation — SQL Injection is not possible.
public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Dictionary<string, object?>> ExecuteQuery(string sql, params SqlParameter[] parameters)
    {
        var results = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
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

    public int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteNonQuery();
    }

    public object? ExecuteScalar(string sql, params SqlParameter[] parameters)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }
}
