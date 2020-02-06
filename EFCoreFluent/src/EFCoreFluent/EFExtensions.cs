#if NETSTANDARD2_1
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Snickler.EFCore
{
    public static class EFExtensions
    {
        /// <summary>
        /// Creates an initial DbCommand object based on a stored procedure name
        /// </summary>
        /// <param name="context">target database context</param>
        /// <param name="storedProcName">target procedure name</param>
        /// <param name="prependDefaultSchema">Prepend the default schema name to <paramref name="storedProcName"/> if explicitly defined in <paramref name="context"/></param>
        /// <param name="commandTimeout">Command timeout in seconds. Default is 30.</param>
        /// <returns></returns>
        public static DbCommand LoadStoredProc(this DbContext context, string storedProcName,
            bool prependDefaultSchema = true, short commandTimeout = 30)
        {
            var cmd = context.Database.GetDbConnection().CreateCommand();
            cmd.CommandTimeout = commandTimeout;

            if (prependDefaultSchema)
            {
#if NETSTANDARD2_1
                var schemaName = context.Model.GetDefaultSchema();
#else
                var schemaName = context.Model.Relational().DefaultSchema;
#endif
                if (schemaName != null)
                {
                    storedProcName = $"{schemaName}.{storedProcName}";
                }
            }

            cmd.CommandText = storedProcName;
            cmd.CommandType = System.Data.CommandType.StoredProcedure;

            return cmd;
        }

        /// <summary>
        /// Creates a DbParameter object and adds it to a DbCommand
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="paramName"></param>
        /// <param name="paramValue"></param>
        /// <param name="configureParam"></param>
        /// <returns></returns>
        public static DbCommand WithSqlParam(this DbCommand cmd, string paramName, object paramValue,
            Action<DbParameter> configureParam = null)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            param.Value = paramValue ?? DBNull.Value;
            configureParam?.Invoke(param);
            cmd.Parameters.Add(param);
            return cmd;
        }

        /// <summary>
        /// Creates a DbParameter object and adds it to a DbCommand
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="paramName"></param>
        /// <param name="configureParam"></param>
        /// <returns></returns>
        public static DbCommand WithSqlParam(this DbCommand cmd, string paramName,
            Action<DbParameter> configureParam = null)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            configureParam?.Invoke(param);
            cmd.Parameters.Add(param);
            return cmd;
        }

        /// <summary>
        /// Adds a SqlParameter to a DbCommand.
        /// This enabled the ability to provide custom types for SQL-parameters.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static DbCommand WithSqlParam(this DbCommand cmd, SqlParameter parameter)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            cmd.Parameters.Add(parameter);

            return cmd;
        }

        /// <summary>
        /// Adds an array of SqlParameters to a DbCommand
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static DbCommand WithSqlParams(this DbCommand cmd, SqlParameter[] parameters)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            cmd.Parameters.AddRange(parameters);

            return cmd;
        }

        public class SprocResults
        {
            private readonly DbDataReader _reader;

            public SprocResults(DbDataReader reader)
            {
                _reader = reader;
            }

            public IList<T> ReadToList<T>() where T : new()
            {
                return MapToList<T>(_reader);
            }

            public T? ReadToValue<T>() where T : struct
            {
                return MapToValue<T>(_reader);
            }

            public Task<bool> NextResultAsync()
            {
                return _reader.NextResultAsync();
            }

            public Task<bool> NextResultAsync(CancellationToken ct)
            {
                return _reader.NextResultAsync(ct);
            }

            public bool NextResult()
            {
                return _reader.NextResult();
            }

            /// <summary>
            /// Retrieves the column values from the stored procedure and maps them to <typeparamref name="T"/>'s properties
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="dr"></param>
            /// <returns>IList&lt;<typeparam name="T">&gt;</typeparam></returns>
            private static IList<T> MapToList<T>(DbDataReader dr) where T : new()
            {
                var objList = new List<T>();
                var props = typeof(T).GetRuntimeProperties().ToList();

                var colMapping = dr.GetColumnSchema()
                    .Where(x => props.Any(y =>
                        string.Equals(y.Name, x.ColumnName, StringComparison.CurrentCultureIgnoreCase)))
                    .ToDictionary(key => key.ColumnName.ToUpper());

                if (!dr.HasRows)
                    return objList;

                while (dr.Read())
                {
                    var obj = new T();
                    foreach (var prop in props)
                    {
                        var upperName = prop.Name.ToUpper();

                        if (!colMapping.ContainsKey(upperName))
                            continue;

                        var column = colMapping[upperName];

                        if (column?.ColumnOrdinal == null)
                            continue;

                        var val = dr.GetValue(column.ColumnOrdinal.Value);
                        prop.SetValue(obj, val == DBNull.Value ? null : val);
                    }

                    objList.Add(obj);
                }

                return objList;
            }

            /// <summary>
            /// Attempts to read the first value of the first row of the result set.
            /// </summary>
            private static T? MapToValue<T>(DbDataReader dr) where T : struct
            {
                if (!dr.HasRows)
                    return new T?();

                if (dr.Read())
                {
                    return dr.IsDBNull(0) ? new T?() : dr.GetFieldValue<T>(0);
                }

                return new T?();
            }
        }

        /// <summary>
        /// Executes a DbDataReader and passes the results to <paramref name="handleResults"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="handleResults"></param>
        /// <param name="commandBehaviour"></param>
        /// <param name="manageConnection"></param>
        /// <returns></returns>
        public static void ExecuteStoredProc(this DbCommand command, Action<SprocResults> handleResults,
            System.Data.CommandBehavior commandBehaviour = System.Data.CommandBehavior.Default,
            bool manageConnection = true)
        {
            if (handleResults == null)
            {
                throw new ArgumentNullException(nameof(handleResults));
            }

            using (command)
            {
                if (manageConnection && command.Connection.State == System.Data.ConnectionState.Closed)
                    command.Connection.Open();
                try
                {
                    using (var reader = command.ExecuteReader(commandBehaviour))
                    {
                        var sprocResults = new SprocResults(reader);
                        handleResults(sprocResults);
                    }
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a DbDataReader asynchronously and passes the results to <paramref name="handleResults"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="handleResults"></param>
        /// <param name="commandBehaviour"></param>
        /// <param name="ct"></param>
        /// <param name="manageConnection"></param>
        /// <returns></returns>
        public static async Task ExecuteStoredProcAsync(this DbCommand command, Action<SprocResults> handleResults,
            System.Data.CommandBehavior commandBehaviour = System.Data.CommandBehavior.Default,
            CancellationToken ct = default, bool manageConnection = true)
        {
            if (handleResults == null)
            {
                throw new ArgumentNullException(nameof(handleResults));
            }

            using (command)
            {
                if (manageConnection && command.Connection.State == System.Data.ConnectionState.Closed)
                    await command.Connection.OpenAsync(ct).ConfigureAwait(false);
                try
                {
                    using (var reader = await command.ExecuteReaderAsync(commandBehaviour, ct)
                        .ConfigureAwait(false))
                    {
                        var sprocResults = new SprocResults(reader);
                        handleResults(sprocResults);
                    }
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a DbDataReader asynchronously and passes the results thru all <paramref name="resultActions"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commandBehaviour"></param>
        /// <param name="ct"></param>
        /// <param name="manageConnection"></param>
        /// <param name="resultActions"></param>
        /// <returns></returns>
        public static async Task ExecuteStoredProcAsync(this DbCommand command,
            System.Data.CommandBehavior commandBehaviour = System.Data.CommandBehavior.Default,
            CancellationToken ct = default, bool manageConnection = true, params Action<SprocResults>[] resultActions)
        {
            if (resultActions == null)
            {
                throw new ArgumentNullException(nameof(resultActions));
            }

            using (command)
            {
                if (manageConnection && command.Connection.State == System.Data.ConnectionState.Closed)
                    await command.Connection.OpenAsync(ct).ConfigureAwait(false);
                try
                {
                    using (var reader = await command.ExecuteReaderAsync(commandBehaviour, ct)
                        .ConfigureAwait(false))
                    {
                        var sprocResults = new SprocResults(reader);

                        foreach (var t in resultActions)
                            t(sprocResults);
                    }
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a non-query.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="manageConnection"></param>
        /// <returns></returns>
        public static int ExecuteStoredNonQuery(this DbCommand command, bool manageConnection = true)
        {
            var numberOfRecordsAffected = -1;

            using (command)
            {
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                {
                    command.Connection.Open();
                }

                try
                {
                    numberOfRecordsAffected = command.ExecuteNonQuery();
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection.Close();
                    }
                }
            }

            return numberOfRecordsAffected;
        }

        /// <summary>
        /// Executes a non-query asynchronously.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct"></param>
        /// <param name="manageConnection"></param>
        /// <returns></returns>
        public static async Task<int> ExecuteStoredNonQueryAsync(this DbCommand command, CancellationToken ct = default,
            bool manageConnection = true)
        {
            var numberOfRecordsAffected = -1;

            using (command)
            {
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                {
                    await command.Connection.OpenAsync(ct).ConfigureAwait(false);
                }

                try
                {
                    numberOfRecordsAffected = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection.Close();
                    }
                }
            }

            return numberOfRecordsAffected;
        }
    }
}