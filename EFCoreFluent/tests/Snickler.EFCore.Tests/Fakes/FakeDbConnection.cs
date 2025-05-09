#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Snickler.EFCore.Tests.Fakes // Ensure this namespace is correct
{
    public class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        public Func<FakeDbCommand>? CreateCommandAction { get; set; }
        public Action? OpenAction { get; set; }
        public Action? CloseAction { get; set; }
        public Func<CancellationToken, Task>? OpenAsyncAction { get; set; }

        public override string ConnectionString { get; set; } = "FakeConnectionString";
        public override string Database => "FakeDatabase";
        public override string DataSource => "FakeDataSource";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { /* No-op */ }
        public override void Close()
        {
            CloseCalled = true;
            _state = ConnectionState.Closed;
            CloseAction?.Invoke();
        }
        public override void Open()
        {
            OpenCalled = true;
            _state = ConnectionState.Open;
            OpenAction?.Invoke();
        }
        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
             OpenAsyncCalled = true;
             OpenAsyncCancellationToken = cancellationToken;
             if (OpenAsyncAction != null)
             {
                await OpenAsyncAction(cancellationToken);
             }
             _state = ConnectionState.Open;
             // Simulating potential cancellation
             cancellationToken.ThrowIfCancellationRequested();
             // await Task.CompletedTask; // Removed, OpenAsyncAction handles await
        }

        protected override DbCommand CreateDbCommand()
        {
             // This calls the protected abstract method below, which we make virtual for easier mocking if needed
             // but prefer using the Action delegate for configuration.
            return CreateDbCommandCore();
        }

        protected virtual FakeDbCommand CreateDbCommandCore() // Return FakeDbCommand
        {
            // Returns a FakeDbCommand, potentially configured via CreateCommandAction
            return CreateCommandAction?.Invoke() ?? new FakeDbCommand(this);
        }

        // Making this virtual allows mocking if needed, but returning null or a basic factory is simpler
        protected override DbProviderFactory? DbProviderFactory => null; // Return null or Mock.Of<DbProviderFactory>() if needed

        // Implement other abstract members if required by testing scenarios
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();

        // --- Test specific helpers / state tracking ---
        public bool OpenCalled { get; private set; } = false;
        public bool CloseCalled { get; private set; } = false;
        public bool OpenAsyncCalled { get; private set; } = false;
        public CancellationToken OpenAsyncCancellationToken { get; private set; }

         // Helper to force state for tests that need specific initial state
         public void ForceState(ConnectionState state)
         {
            _state = state;
         }
    }
} 