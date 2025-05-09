#nullable enable

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Snickler.EFCore
{
    public static partial class EFExtensions
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
                var schemaName = context.Model.GetDefaultSchema();
                if (!string.IsNullOrEmpty(schemaName))
                {
                    storedProcName = $"{schemaName}.{storedProcName}";
                }
            }

            cmd.CommandText = storedProcName;
            cmd.CommandType = CommandType.StoredProcedure;

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
            Action<DbParameter>? configureParam = null)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            if (paramValue == null)
            {
                param.Value = DBNull.Value;
            }
            else
            {
                param.Value = paramValue;
            }
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
            Action<DbParameter>? configureParam = null)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != CommandType.StoredProcedure)
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
        public static DbCommand WithSqlParam(this DbCommand cmd, IDbDataParameter parameter)
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
        public static DbCommand WithSqlParams(this DbCommand cmd, IDbDataParameter[] parameters)
        {
            if (string.IsNullOrEmpty(cmd.CommandText) && cmd.CommandType != System.Data.CommandType.StoredProcedure)
                throw new InvalidOperationException("Call LoadStoredProc before using this method");

            cmd.Parameters.AddRange(parameters);

            return cmd;
        }

        public partial class SprocResults : IDisposable
        {
            private readonly DbDataReader _reader;
            private readonly bool _closeReaderOnDispose;
            private bool _disposed = false;

            // Static dummy methods for generator triggering
            public static void ReadToList_Dummy<T>() where T : new() { }
            public static void ReadToValueTupleList_Dummy<TValueTuple>() where TValueTuple : struct { }

            public SprocResults(DbDataReader reader, bool closeReaderOnDispose = true)
            {
                _reader = reader ?? throw new ArgumentNullException(nameof(reader));
                _closeReaderOnDispose = closeReaderOnDispose;
            }

            [RequiresUnreferencedCode("SprocResults.ReadToList<T> uses reflection. For AOT compatibility, ensure the source generator is active for type T, or use a T that is appropriately annotated.")]
            public IList<T> ReadToList<T>() where T : new()
            {
                // This method will be implemented by the source generator in a partial class.
                // If the generator doesn't run or doesn't find an invocation for T, this will lead to a compile error
                // or a runtime error if this base method had a fallback (which it currently doesn't explicitly).
                return GeneratedReadToListDispatch<T>();
            }

            [RequiresUnreferencedCode("SprocResults.ReadToValueTupleList<TValueTuple> uses reflection. For AOT compatibility, ensure the source generator is active for type TValueTuple, or use a TValueTuple that is appropriately annotated.")]
            public IList<TValueTuple> ReadToValueTupleList<TValueTuple>() where TValueTuple : struct
            {
                // This method will be implemented by the source generator in a partial class.
                return GeneratedReadToValueTupleListDispatch<TValueTuple>();
            }

            [RequiresUnreferencedCode("DataTable.Load uses reflection and is not AOT compatible.")]
            public DataTable ReadToDataTable()
            {
                var dataTable = new DataTable();
                dataTable.Load(_reader);
                return dataTable;
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
            /// <returns>IList&lt;<typeparam name="T"/>&gt;</typeparam></returns>
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

            public void Dispose()
            {
                if (!_disposed)
                {
                    _reader.Dispose();
                    _disposed = true;
                }
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
            CommandBehavior commandBehaviour = CommandBehavior.Default,
            bool manageConnection = true)
        {
            if (handleResults == null)
            {
                throw new ArgumentNullException(nameof(handleResults));
            }

            using (command)
            {
                if (manageConnection && command.Connection!.State == ConnectionState.Closed)
                    command.Connection!.Open();
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
                        command.Connection!.Close();
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
                if (manageConnection && command.Connection!.State == System.Data.ConnectionState.Closed)
                    await command.Connection!.OpenAsync(ct).ConfigureAwait(false);
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
                        command.Connection!.Close();
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
            CommandBehavior commandBehaviour = CommandBehavior.Default,
            CancellationToken ct = default, bool manageConnection = true, params Action<SprocResults>[] resultActions)
        {
            if (resultActions == null)
            {
                throw new ArgumentNullException(nameof(resultActions));
            }

            using (command)
            {
                if (manageConnection && command.Connection!.State == ConnectionState.Closed)
                    await command.Connection!.OpenAsync(ct).ConfigureAwait(false);
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
                        command.Connection!.Close();
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
                if (command.Connection!.State == ConnectionState.Closed)
                {
                    command.Connection!.Open();
                }

                try
                {
                    numberOfRecordsAffected = command.ExecuteNonQuery();
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection!.Close();
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
                if (command.Connection!.State == ConnectionState.Closed)
                {
                    await command.Connection!.OpenAsync(ct).ConfigureAwait(false);
                }

                try
                {
                    numberOfRecordsAffected = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    if (manageConnection)
                    {
                        command.Connection!.Close();
                    }
                }
            }

            return numberOfRecordsAffected;
        }
    }

    // Internal class to trigger source generator for specific types
    // This class and its methods/nested types are purely to ensure the source generator
    // is triggered for specific types during compilation. It is not intended for runtime use.
    internal static class _GeneratorTrigger_
    {
        // These methods don't need to be called at runtime if this class is compiled
        // into the main assembly; the generator analyzes syntax trees.
        public static void EnsureGeneratorRunsForPocos()
        {
            EFExtensions.SprocResults.ReadToList_Dummy<Snickler.EFCore.TestData.SimplePoco>();
            EFExtensions.SprocResults.ReadToList_Dummy<Snickler.EFCore.TestData.PocoWithAttributes>();
            EFExtensions.SprocResults.ReadToList_Dummy<Snickler.EFCore.TestData.PocoWithDateAndTime>();
            EFExtensions.SprocResults.ReadToList_Dummy<Snickler.EFCore.TestData.PocoTypeForEmptySet>();
            EFExtensions.SprocResults.ReadToList_Dummy<Snickler.EFCore.TestData.PocoTypeForNoRows>();
        }

        public static void EnsureGeneratorRunsForValueTuples()
        {
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(int, string)>();
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(int /*Id*/, string /*Value*/)>(); // Names in tuple don't change type identity for typeof
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(string?, decimal)>();
            // EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(System.DateTime, System.TimeOnly)>(); // Keep if a DateTime/TimeOnly combo is used from System namespace
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<System.ValueTuple<int>>(); // Equivalent to (int)
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(System.DateOnly, System.TimeOnly, int)>(); // The one that was generated
            EFExtensions.SprocResults.ReadToValueTupleList_Dummy<(int, System.DateOnly, System.TimeOnly)>(); // The one from the failing test
        }
    }
}
