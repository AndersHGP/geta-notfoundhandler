using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Geta.NotFoundHandler.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Geta.NotFoundHandler.Data
{
    public class SqlDataExecutor : IDataExecutor
    {
        private readonly ILogger<SqlDataExecutor> _logger;
        private readonly string _connectionString;

        public SqlDataExecutor(
            IOptions<NotFoundHandlerOptions> options,
            ILogger<SqlDataExecutor> logger)
        {
            _connectionString = options.Value.ConnectionString;
            _logger = logger;
        }

        public DataSet ExecuteSql(string sqlCommand, params IDbDataParameter[] parameters)
        {
            var ds = new DataSet();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var command = CreateCommand(connection, sqlCommand, parameters);
                using var da = new SqlDataAdapter(command);
                da.Fill(ds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"An error occurred in the ExecuteSQL method with the following sql: {sqlCommand}",
                    ex);
            }

            return ds;
        }

        public bool ExecuteNonQuery(string sqlCommand, params IDbDataParameter[] parameters)
        {
            var success = true;

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var command = CreateCommand(connection, sqlCommand, parameters);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                success = false;
                _logger.LogError(
                    $"An error occurred in the ExecuteSQL method with the following sql: {sqlCommand}",
                    ex);
            }

            return success;
        }

        public int ExecuteScalar(string sqlCommand)
        {
            int result;
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var command = CreateCommand(connection, sqlCommand);
                result = (int)command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                result = 0;
                _logger.LogError(
                    $"An error occurred in the ExecuteScalar method with the following sql: {sqlCommand}",
                    ex);
            }

            return result;
        }

        public int ExecuteStoredProcedure(string sqlCommand, int defaultReturnValue = -1)
        {
            var value = defaultReturnValue;
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var command = CreateCommand(connection, sqlCommand);
                command.Parameters.Add(CreateReturnParameter());
                command.CommandText = sqlCommand;
                command.CommandType = CommandType.StoredProcedure;
                command.ExecuteNonQuery();
                value = Convert.ToInt32(GetReturnValue(command).ToString());
            }
            catch (SqlException)
            {
                _logger.LogInformation("Stored procedure not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while running stored procedure.", ex);
            }
            return value;
        }

        public DbParameter CreateParameter(string parameterName, DbType dbType, int size)
        {
            var parameter = new SqlParameter
            {
                ParameterName = parameterName,
                DbType = dbType,
                Direction = ParameterDirection.Input,
                Size = size == 0 ? 1 : size
            };
            return parameter;
        }

        private SqlParameter CreateReturnParameter()
        {
            var parameter = new SqlParameter
            {
                ParameterName = "@ReturnValue",
                DbType = DbType.Int32,
                Direction = ParameterDirection.ReturnValue,
            };
            return parameter;
        }

        public static int GetReturnValue(DbCommand cmd)
        {
            var parameter = cmd.Parameters["@ReturnValue"];
            return Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture);
        }

        private SqlCommand CreateCommand(SqlConnection connection, string sqlCommand, params IDbDataParameter[] parameters)
        {
            var command = connection.CreateCommand(sqlCommand);

            if (parameters != null)
            {
                foreach (var dbDataParameter in parameters)
                {
                    var parameter = (SqlParameter)dbDataParameter;
                    command.Parameters.Add(parameter);
                }
            }

            command.CommandType = CommandType.Text;

            return command;
        }
    }

    public interface IDataExecutor
    {
        DataSet ExecuteSql(string sqlCommand, params IDbDataParameter[] parameters);
        bool ExecuteNonQuery(string sqlCommand, params IDbDataParameter[] parameters);
        int ExecuteScalar(string sqlCommand);
        int ExecuteStoredProcedure(string sqlCommand, int defaultReturnValue = -1);
        DbParameter CreateParameter(string parameterName, DbType dbType, int size);
    }

    public static class SqlConnectionExtensions
    {
        public static SqlCommand CreateCommand(this SqlConnection connection, string cmdText)
        {
            var command = connection.CreateCommand();
            command.CommandText = cmdText;
            return command;
        }
    }
}
