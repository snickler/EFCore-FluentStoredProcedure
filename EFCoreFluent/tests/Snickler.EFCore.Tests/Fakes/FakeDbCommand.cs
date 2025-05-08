using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Snickler.EFCore.Tests.Fakes
{
    // Basic implementation, needs refinement based on usage
    public class FakeDbCommand : DbCommand
    {
        private readonly FakeDbConnection _connection;
        private readonly FakeDbParameterCollection _parameters = new FakeDbParameterCollection();

        public FakeDbCommand(FakeDbConnection connection)
        {
            _connection = connection;
            // Default timeout mimics EF Core default
            CommandTimeout = 30; 
        }

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get => _connection; set => throw new NotSupportedException("Cannot set connection on FakeDbCommand"); } 
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; } // Can be null

        public override void Cancel() { IsCancelled = true; }
        public override int ExecuteNonQuery()
        {
            ExecuteNonQueryCalled = true;
            return ExecuteNonQueryReturnValue;
        }
        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            await Task.Yield(); // Simulate async
            ExecuteNonQueryAsyncCalled = true;
            ExecuteNonQueryAsyncCancellationToken = cancellationToken;
            IsCancelled = cancellationToken.IsCancellationRequested;
            return ExecuteNonQueryReturnValue;
        }
        public override object? ExecuteScalar() => throw new NotImplementedException(); // Implement if needed
        public override void Prepare() { /* No-op */ }

        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
             ExecuteReaderCalled = true;
             ExecuteReaderBehavior = behavior;
             return ExecuteReaderAction?.Invoke(behavior) ?? new FakeDbDataReader(null, null); // Default empty reader
        }
         protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            await Task.Yield(); // Simulate async
            ExecuteReaderAsyncCalled = true;
            ExecuteReaderAsyncBehavior = behavior;
            ExecuteReaderAsyncCancellationToken = cancellationToken;
            IsCancelled = cancellationToken.IsCancellationRequested;
            return ExecuteReaderAction?.Invoke(behavior) ?? new FakeDbDataReader(null, null); // Default empty reader
        }

        // --- Test specific helpers / state tracking ---
        public int ExecuteNonQueryReturnValue { get; set; } = 0;
        public Func<CommandBehavior, FakeDbDataReader>? ExecuteReaderAction { get; set; }
        public Action<bool>? DisposeAction { get; set; }
        
        public bool ExecuteNonQueryCalled { get; private set; } = false;
        public bool ExecuteNonQueryAsyncCalled { get; private set; } = false;
        public CancellationToken ExecuteNonQueryAsyncCancellationToken { get; private set; }
        public bool ExecuteReaderCalled { get; private set; } = false;
        public CommandBehavior ExecuteReaderBehavior { get; private set; }
        public bool ExecuteReaderAsyncCalled { get; private set; } = false;
        public CommandBehavior ExecuteReaderAsyncBehavior { get; private set; }
        public CancellationToken ExecuteReaderAsyncCancellationToken { get; private set; }
        public bool DisposedCalled { get; private set; } = false;
        public bool IsCancelled { get; private set; } = false;


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposedCalled = true;
                DisposeAction?.Invoke(disposing);
            }
            base.Dispose(disposing);
        }
    }
} 