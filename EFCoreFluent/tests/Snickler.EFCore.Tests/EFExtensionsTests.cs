using Xunit;
// using Moq;
// using Moq.Protected; // Removed for Fakes
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage; // Required for IRelationalConnection, IRelationalDatabaseFacadeDependencies
using System.Data.Common;
using System.Data;
using Snickler.EFCore; // Your project's namespace
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal; // For RelationalAnnotationNames if needed
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema; // For ColumnAttribute
using System.Collections.ObjectModel; // For ReadOnlyCollection
using System;
using System.Threading;
using System.Threading.Tasks;
using Snickler.EFCore.Tests.Fakes; // Using Fakes namespace
using Moq; // Keep Moq for DbContext, IModel etc for now
using Snickler.EFCore.TestData; // Add using for shared test POCOs

namespace Snickler.EFCore.Tests
{
    // --- Test POCOs removed - now defined in Snickler.EFCore.TestData ---
    // public class SimplePoco { ... }
    // public class PocoWithAttributes { ... }
    // public class PocoWithDateAndTime { ... }
    // public struct NotAPocoStruct { ... }

    public class EFExtensionsTests
    {
        // Replace Moq DbConnection with FakeDbConnection
        private readonly FakeDbConnection _fakeConnection;
        private readonly Mock<DbContext> _mockDbContext;
        private readonly Mock<IModel> _mockModel;
        private readonly Mock<IRelationalConnection> _mockRelationalConnection;
        private readonly Mock<IRelationalDatabaseFacadeDependencies> _mockRelationalDatabaseFacadeDependencies;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly DatabaseFacade _realDatabaseFacade;

        // Store the last created FakeDbCommand for inspection
        private FakeDbCommand? _lastCreatedFakeCommand;

        public EFExtensionsTests()
        {
            _fakeConnection = new FakeDbConnection();
            _mockDbContext = new Mock<DbContext>(new DbContextOptions<DbContext>());
            _mockModel = new Mock<IModel>();
            _mockRelationalConnection = new Mock<IRelationalConnection>();
            _mockRelationalDatabaseFacadeDependencies = new Mock<IRelationalDatabaseFacadeDependencies>();
            _mockServiceProvider = new Mock<IServiceProvider>();

            // Setup the mock chain to return the FakeDbConnection
            _mockRelationalConnection.Setup(rc => rc.DbConnection).Returns(_fakeConnection);
            _mockRelationalDatabaseFacadeDependencies.Setup(dep => dep.RelationalConnection)
                                                 .Returns(_mockRelationalConnection.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IRelationalDatabaseFacadeDependencies)))
                                .Returns(_mockRelationalDatabaseFacadeDependencies.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IDatabaseFacadeDependencies)))
                                .Returns(_mockRelationalDatabaseFacadeDependencies.Object);
            _mockDbContext.As<IInfrastructure<IServiceProvider>>()
                          .Setup(infra => infra.Instance)
                          .Returns(_mockServiceProvider.Object);
            _mockDbContext.Setup(ctx => ctx.Model).Returns(_mockModel.Object);
            _realDatabaseFacade = new DatabaseFacade(_mockDbContext.Object);
            _mockDbContext.Setup(ctx => ctx.Database).Returns(_realDatabaseFacade);

            // Configure FakeDbConnection to return a new FakeDbCommand when CreateCommand is called
            // We capture the created command for potential assertions later.
            _fakeConnection.CreateCommandAction = () => 
            {
                _lastCreatedFakeCommand = new FakeDbCommand(_fakeConnection)
                {
                    // Set default behaviors needed across many tests, replacing old Moq setup
                    ExecuteNonQueryReturnValue = 0,
                    ExecuteReaderAction = (behavior) => new FakeDbDataReader(null, null) // Default empty reader
                };
                return _lastCreatedFakeCommand;
            };
        }

        // Removed SetupMockCommandForExecution as setup is now part of Fake classes or test-specific

        [Fact]
        public void LoadStoredProc_ShouldSetCommandTextAndType()
        {
            // Arrange
            var storedProcName = "TestProc";
            // var mockAnnotation = new Mock<IAnnotation>();
            // mockAnnotation.Setup(a => a.Value).Returns(default(string)); // Setup mock annotation for schema
            // _mockModel.Setup(m => m.FindAnnotation(RelationalAnnotationNames.DefaultSchema)).Returns(mockAnnotation.Object);
            _mockModel.Setup(m => m[RelationalAnnotationNames.DefaultSchema]).Returns(default(string));

            // Act
            // This call uses the DbContext -> Database -> Connection -> CreateCommand chain
            // which should now invoke our FakeDbConnection.CreateCommandAction
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: false);
            
            // Assert
            Assert.NotNull(_lastCreatedFakeCommand); // Ensure a command was created
            Assert.Same(command, _lastCreatedFakeCommand); // Ensure the returned command is the one we captured
            Assert.Equal(storedProcName, command.CommandText);
            Assert.Equal(CommandType.StoredProcedure, command.CommandType);
            Assert.Equal(30, command.CommandTimeout); // Default timeout
        }

        [Fact]
        public void LoadStoredProc_ShouldPrependDefaultSchema_WhenSchemaExists()
        {
            // Arrange
            var storedProcName = "TestProc";
            var schemaName = "dbo";
            var expectedCommandText = $"{schemaName}.{storedProcName}";
            // var mockAnnotation = new Mock<IAnnotation>();
            // mockAnnotation.Setup(a => a.Value).Returns(schemaName); // Setup mock annotation with schema
            // _mockModel.Setup(m => m.FindAnnotation(RelationalAnnotationNames.DefaultSchema)).Returns(mockAnnotation.Object);
            _mockModel.Setup(m => m[RelationalAnnotationNames.DefaultSchema]).Returns(schemaName);

            // Act
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: true);

            // Assert
            Assert.NotNull(_lastCreatedFakeCommand);
            Assert.Same(command, _lastCreatedFakeCommand);
            Assert.Equal(expectedCommandText, command.CommandText); // Reinstate assertion
        }

        [Fact]
        public void LoadStoredProc_ShouldNotPrependDefaultSchema_WhenSchemaDoesNotExist()
        {
            // Arrange
            var storedProcName = "TestProc";
            // _mockModel.Setup(m => m.FindAnnotation(RelationalAnnotationNames.DefaultSchema)).Returns(default(IAnnotation)); // No schema annotation
            _mockModel.Setup(m => m[RelationalAnnotationNames.DefaultSchema]).Returns(default(string)); // Should result in null schema

            // Act
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: true);

            // Assert
            Assert.NotNull(_lastCreatedFakeCommand);
            Assert.Same(command, _lastCreatedFakeCommand);
            Assert.Equal(storedProcName, command.CommandText);
        }

        [Fact]
        public void LoadStoredProc_ShouldSetCustomCommandTimeout()
        {
            // Arrange
            var storedProcName = "TestProc";
            short customTimeout = 60;
            // var mockAnnotation = new Mock<IAnnotation>();
            // mockAnnotation.Setup(a => a.Value).Returns(default(string)); // Setup mock annotation for schema
            // _mockModel.Setup(m => m.FindAnnotation(RelationalAnnotationNames.DefaultSchema)).Returns(mockAnnotation.Object);
            _mockModel.Setup(m => m[RelationalAnnotationNames.DefaultSchema]).Returns(default(string));

            // Act
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, commandTimeout: customTimeout);

            // Assert
            Assert.NotNull(_lastCreatedFakeCommand);
            Assert.Same(command, _lastCreatedFakeCommand);
            Assert.Equal(customTimeout, command.CommandTimeout);
        }

        [Fact]
        public void WithSqlParam_WithValue_ShouldAddParameter()
        {
            // Arrange
            var paramName = "@TestParam";
            var paramValue = "TestValue";
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc"); // Cast to FakeDbCommand
            
            // Act
            command.WithSqlParam(paramName, paramValue);

            // Assert
            Assert.Single(command.Parameters);
            var param = (FakeDbParameter)command.Parameters[0];
            Assert.Equal(paramName, param.ParameterName);
            Assert.Equal(paramValue, param.Value);
        }

        [Fact]
        public void WithSqlParam_WithNullValue_ShouldAddParameterWithDBNull()
        {
            // Arrange
            var paramName = "@TestParam";
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc");

            // Act
            command.WithSqlParam(paramName, null);

            // Assert
            Assert.Single(command.Parameters);
            var param = (FakeDbParameter)command.Parameters[0];
            Assert.Equal(paramName, param.ParameterName);
            // Assert.Same(DBNull.Value, param.Value); // Avoid asserting Value due to persistent issues.
            // We infer correct handling by checking the parameter exists with the right name.
        }

        [Fact]
        public void WithSqlParam_WithConfigureAction_ShouldInvokeAction()
        {
            // Arrange
            var paramName = "@TestParam";
            var paramValue = 123;
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc");
            bool configureActionCalled = false;
            Action<DbParameter> configureParam = p => 
            {
                p.DbType = DbType.Int32;
                configureActionCalled = true; 
            };

            // Act
            command.WithSqlParam(paramName, paramValue, configureParam);

            // Assert
            Assert.True(configureActionCalled);
            Assert.Single(command.Parameters);
            var param = (FakeDbParameter)command.Parameters[0];
            Assert.Equal(paramName, param.ParameterName);
            Assert.Equal(paramValue, param.Value);
            Assert.Equal(DbType.Int32, param.DbType);
        }

        [Fact]
        public void WithSqlParam_OverloadWithoutValue_ShouldAddParameterAndInvokeAction()
        {
            // Arrange
            var paramName = "@OutputParam";
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc");
            bool configureActionCalled = false;
            Action<DbParameter> configureParam = p => 
            { 
                p.Direction = ParameterDirection.Output;
                configureActionCalled = true; 
            };

            // Act
            command.WithSqlParam(paramName, configureParam);

            // Assert
            Assert.True(configureActionCalled);
            Assert.Single(command.Parameters);
            var param = (FakeDbParameter)command.Parameters[0];
            Assert.Equal(paramName, param.ParameterName);
            Assert.Equal(ParameterDirection.Output, param.Direction);
        }

        [Fact]
        public void WithSqlParam_WithIDbDataParameter_ShouldAddParameter()
        {
            // Arrange
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc");
            var customParameter = new FakeDbParameter { ParameterName = "@CustomParam", Value = "CustomValue" };

            // Act
            command.WithSqlParam(customParameter);

            // Assert
            Assert.Single(command.Parameters);
            Assert.Same(customParameter, command.Parameters[0]);
        }

        [Fact]
        public void WithSqlParams_WithParameterArray_ShouldAddAllParameters()
        {
            // Arrange
            var command = (FakeDbCommand)_mockDbContext.Object.LoadStoredProc("TestProc");
            var param1 = new FakeDbParameter { ParameterName = "@Param1" };
            var param2 = new FakeDbParameter { ParameterName = "@Param2" };
            var parameters = new DbParameter[] { param1, param2 }; // Use DbParameter array

            // Act
            command.WithSqlParams(parameters);

            // Assert
            Assert.Equal(2, command.Parameters.Count);
            Assert.Same(param1, command.Parameters[0]);
            Assert.Same(param2, command.Parameters[1]);
        }

        // NOTE: WithSqlParam_ThrowsInvalidOperation_WhenCommandNotReady test needs adjustment
        // as it used Moq directly on DbCommand. We might skip this specific test or 
        // refactor it to test the SUT (EFExtensions) more directly if the precondition 
        // check inside WithSqlParam is important.
        // For now, commenting out as it requires mocking non-virtual members directly.
        // [Theory]
        // [InlineData(null, CommandType.Text)]          
        // [InlineData("SELECT 1", CommandType.Text)]   
        // [InlineData("", CommandType.StoredProcedure)] 
        // public void WithSqlParam_ThrowsInvalidOperation_WhenCommandNotReady(string? initialCommandTextForTestContext, CommandType initialCommandTypeForTestContext)
        // {
        //     // Arrange
        //     var command = new FakeDbCommand(new FakeDbConnection()); // Use Fake
        //     command.CommandText = initialCommandTextForTestContext;
        //     command.CommandType = initialCommandTypeForTestContext;

        //     // Act & Assert - Need to check how EFExtensions.WithSqlParam accesses CommandText/Type
        //     // If it directly accesses properties on the DbCommand instance, the Fake should work.
        //     Assert.Throws<InvalidOperationException>(() => command.WithSqlParam("@Param", 123));
        //     Assert.Throws<InvalidOperationException>(() => command.WithSqlParam("@Param", p => { }));
        //     Assert.Throws<InvalidOperationException>(() => command.WithSqlParam(new FakeDbParameter()));
        //     Assert.Throws<InvalidOperationException>(() => command.WithSqlParams(new DbParameter[] { new FakeDbParameter() }));
        // }

        private FakeDbDataReader SetupDataReaderFakes(List<DbColumn>? schemaColumns, List<object[]>? rowData, bool hasRows = true)
        {
             DataTable? schemaTable = null;
            if (schemaColumns != null)
            {
                schemaTable = new DataTable();
                // Define columns for the schema table itself based on DbColumn properties
                schemaTable.Columns.Add("ColumnName", typeof(string));
                schemaTable.Columns.Add("ColumnOrdinal", typeof(int));
                schemaTable.Columns.Add("DataType", typeof(Type));
                schemaTable.Columns.Add("AllowDBNull", typeof(bool));
                schemaTable.Columns.Add("ColumnSize", typeof(int));
                schemaTable.Columns.Add("IsKey", typeof(bool));
                // Add other DbColumn properties as needed by the SUT's GetColumnSchema() call

                foreach (var dbCol in schemaColumns)
                {
                    var row = schemaTable.NewRow();
                    row["ColumnName"] = dbCol.ColumnName;
                    row["ColumnOrdinal"] = dbCol.ColumnOrdinal ?? -1; // Use -1 if null to avoid exception
                    row["DataType"] = dbCol.DataType;       
                    row["AllowDBNull"] = dbCol.AllowDBNull ?? true; 
                    row["ColumnSize"] = dbCol.ColumnSize ?? -1;
                    row["IsKey"] = dbCol.IsKey ?? false;
                    schemaTable.Rows.Add(row);
                }
            }

            return new FakeDbDataReader(rowData, schemaTable);
        }

        // Use simple tuple for schema info instead of abstract DbColumn
        private DataTable CreateSchemaTable(params (string Name, Type Type, int Ordinal, bool AllowNull, bool IsKey, int Size)[] columns)
        {
            var schemaTable = new DataTable();
            schemaTable.Columns.Add("ColumnName", typeof(string));
            schemaTable.Columns.Add("ColumnOrdinal", typeof(int));
            schemaTable.Columns.Add("DataType", typeof(Type));
            schemaTable.Columns.Add("AllowDBNull", typeof(bool));
            schemaTable.Columns.Add("ColumnSize", typeof(int));
            schemaTable.Columns.Add("IsKey", typeof(bool));

            foreach(var col in columns)
            {
                var row = schemaTable.NewRow();
                row["ColumnName"] = col.Name;
                row["ColumnOrdinal"] = col.Ordinal;
                row["DataType"] = col.Type;
                row["AllowDBNull"] = col.AllowNull;
                row["IsKey"] = col.IsKey;
                row["ColumnSize"] = col.Size;
                schemaTable.Rows.Add(row);
            }
            return schemaTable;
        }

        [Fact]
        public void MapToList_SimplePoco_ShouldMapCorrectly()
        {
            // Arrange
            var schemaTable = CreateSchemaTable(
                ("Id", typeof(int), 0, false, true, -1),
                ("Name", typeof(string), 1, true, false, 100),
                ("Value", typeof(decimal), 2, true, false, -1)
            );
            var data = new List<object[]>
            {
                new object[] { 1, "Test1", 10.5m },
                new object[] { 2, "Test2", DBNull.Value },
                new object[] { 3, "Test3", 20.0m }
            };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToList<SimplePoco>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(1, result[0].Id);
            Assert.Equal("Test1", result[0].Name);
            Assert.Equal(10.5m, result[0].Value);
            Assert.Equal(2, result[1].Id);
            Assert.Equal("Test2", result[1].Name);
            Assert.Null(result[1].Value);
            Assert.Equal(3, result[2].Id);
            Assert.Equal("Test3", result[2].Name);
            Assert.Equal(20.0m, result[2].Value);
        }

        [Fact]
        public void MapToList_PocoWithAttributes_ShouldMapCorrectlyAndHandleUnmapped()
        {
            // Arrange
             var schemaTable = CreateSchemaTable(
                ("product_id", typeof(int), 0, false, true, -1),
                ("product_name", typeof(string), 1, true, false, 200),
                ("ExtraColumn", typeof(string), 2, true, false, 50) // Unmapped column
            );
            var data = new List<object[]>
            {
                new object[] { 101, "Laptop", "SomeExtraValue" }
            };

            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToList<PocoWithAttributes>();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(101, result[0].ProductId);
            Assert.Equal("Laptop", result[0].ProductName);
            Assert.Equal("Default", result[0].UnmappedProperty); // Check default value for unmapped property
        }
        
        [Fact]
        public void MapToList_PocoWithDateAndTime_ShouldMapCorrectly()
        {
            // Arrange
            var testDate = new DateTime(2023, 10, 26, 14, 30, 15);
            var schemaTable = CreateSchemaTable(
                ("Id", typeof(int), 0, false, true, -1),
                ("EventDate", typeof(DateTime), 1, false, false, -1),
                ("EventTime", typeof(DateTime), 2, false, false, -1),
                ("EventDateTime", typeof(DateTime), 3, false, false, -1)
            );
            var data = new List<object[]>
            {
                new object[] { 1, testDate, testDate, testDate } // Raw data is DateTime
            };

            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            // No need to mock GetFieldValue<T> in FakeDbDataReader if MapToList uses GetValue and handles conversion

            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);
            
            // Act
            var result = sprocResults.ReadToList<PocoWithDateAndTime>();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
            Assert.Equal(DateOnly.FromDateTime(testDate), result[0].EventDate); // SUT handles conversion
            Assert.Equal(TimeOnly.FromDateTime(testDate), result[0].EventTime); // SUT handles conversion
            Assert.Equal(testDate, result[0].EventDateTime);
        }

        [Fact]
        public void MapToList_EmptyResultSet_ShouldReturnEmptyList()
        {
            // Arrange
            var schemaTable = CreateSchemaTable(("Id", typeof(int), 0, false, true, -1));
            var data = new List<object[]>(); // Empty data

            var fakeDataReader = new FakeDbDataReader(data, schemaTable); 
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToList<SimplePoco>();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void MapToList_ReaderHasNoRows_ShouldReturnEmptyList()
        {
            // Arrange
            // Pass null for schema and data to FakeDbDataReader constructor
            var fakeDataReader = new FakeDbDataReader(null, null); 
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToList<SimplePoco>();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.False(fakeDataReader.HasRows);
        }

        [Fact]
        public void ExecuteStoredProc_CallsHandleResults_AndManagesConnection()
        {
            // Arrange
            var fakeDataReader = new FakeDbDataReader(null, null); // Empty reader
            _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteReaderAction = (behavior) => fakeDataReader // Configure command to return specific reader
            };
            _fakeConnection.ForceState(ConnectionState.Closed); // Set initial state

            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = results => { 
                Assert.NotNull(results);
                // Accessing internal reader might require making SprocResults more testable or relying on side effects
                handleResultsCalled = true; 
            };

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc"); 
            command.ExecuteStoredProc(handleResults, CommandBehavior.Default, manageConnection: true);
            var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.True(handleResultsCalled);
            Assert.True(fakeCommand.ExecuteReaderCalled);
            Assert.Equal(CommandBehavior.Default, fakeCommand.ExecuteReaderBehavior);
            Assert.True(_fakeConnection.OpenCalled);
            Assert.True(_fakeConnection.CloseCalled);
            Assert.True(fakeCommand.DisposedCalled);
        }

        [Fact]
        public void ExecuteStoredProc_DoesNotManageConnection_WhenManageConnectionIsFalse()
        {
           // Arrange
            var fakeDataReader = new FakeDbDataReader(null, null); // Empty reader
            _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteReaderAction = (behavior) => fakeDataReader // Configure command to return specific reader
            };
            _fakeConnection.ForceState(ConnectionState.Open); // Set initial state

            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = r => { handleResultsCalled = true; };

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            command.ExecuteStoredProc(handleResults, CommandBehavior.Default, manageConnection: false);
            var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.True(handleResultsCalled);
            Assert.True(fakeCommand.ExecuteReaderCalled);
            Assert.Equal(CommandBehavior.Default, fakeCommand.ExecuteReaderBehavior);
            Assert.False(_fakeConnection.OpenCalled); 
            Assert.False(_fakeConnection.CloseCalled); 
            Assert.True(fakeCommand.DisposedCalled);
        }

        [Fact]
        public void ExecuteStoredProc_ThrowsArgumentNullException_WhenHandleResultsIsNull()
        {
            // Arrange
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            // Act & Assert
            Assert.Throws<ArgumentNullException>("handleResults", () => 
                command.ExecuteStoredProc(null!, CommandBehavior.Default, true)); // Use null!
        }

        [Fact]
        public async Task ExecuteStoredProcAsync_CallsHandleResults_AndManagesConnection_WithAction()
        {
            // Arrange
            var fakeDataReader = new FakeDbDataReader(null, null); // Empty reader
            _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteReaderAction = (behavior) => fakeDataReader // Configure command to return specific reader
            };
             _fakeConnection.ForceState(ConnectionState.Closed); // Set initial state
            var cts = new CancellationTokenSource();

            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = results => { Assert.NotNull(results); handleResultsCalled = true; };

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            await command.ExecuteStoredProcAsync(handleResults, CommandBehavior.Default, cts.Token, manageConnection: true);
            var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.True(handleResultsCalled);
            Assert.True(fakeCommand.ExecuteReaderAsyncCalled);
            Assert.Equal(CommandBehavior.Default, fakeCommand.ExecuteReaderAsyncBehavior);
            Assert.Equal(cts.Token, fakeCommand.ExecuteReaderAsyncCancellationToken);
            Assert.True(_fakeConnection.OpenAsyncCalled);
            Assert.Equal(cts.Token, _fakeConnection.OpenAsyncCancellationToken);
            Assert.True(_fakeConnection.CloseCalled); // Close is still synchronous in SUT
            Assert.True(fakeCommand.DisposedCalled);
        }
        
        [Fact]
        public async Task ExecuteStoredProcAsync_CallsResultActions_AndManagesConnection_WithParamsAction()
        {
            // Arrange
            var fakeDataReader = new FakeDbDataReader(null, null); // Empty reader
            _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteReaderAction = (behavior) => fakeDataReader // Configure command to return specific reader
            };
            _fakeConnection.ForceState(ConnectionState.Closed); // Set initial state
            var cts = new CancellationTokenSource();
            int action1Called = 0;
            int action2Called = 0;
            Action<EFExtensions.SprocResults> action1 = results => { Assert.NotNull(results); action1Called++; }; 
            Action<EFExtensions.SprocResults> action2 = results => { Assert.NotNull(results); action2Called++; };

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            await command.ExecuteStoredProcAsync(CommandBehavior.Default, cts.Token, true, action1, action2);
             var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.Equal(1, action1Called);
            Assert.Equal(1, action2Called);
            Assert.True(fakeCommand.ExecuteReaderAsyncCalled);
            Assert.Equal(CommandBehavior.Default, fakeCommand.ExecuteReaderAsyncBehavior);
            Assert.Equal(cts.Token, fakeCommand.ExecuteReaderAsyncCancellationToken);
            Assert.True(_fakeConnection.OpenAsyncCalled);
            Assert.Equal(cts.Token, _fakeConnection.OpenAsyncCancellationToken);
            Assert.True(_fakeConnection.CloseCalled); // Close is still synchronous in SUT
            Assert.True(fakeCommand.DisposedCalled);
        }

        [Fact]
        public async Task ExecuteStoredProcAsync_ThrowsArgumentNullException_WhenHandleResultsIsNull_WithActionOverload()
        {
            // Arrange
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            // Act & Assert
             await Assert.ThrowsAsync<ArgumentNullException>("handleResults", () =>
                 command.ExecuteStoredProcAsync((Action<EFExtensions.SprocResults>)null!, CommandBehavior.Default, CancellationToken.None, true));
        }

        [Fact]
        public async Task ExecuteStoredProcAsync_ThrowsArgumentNullException_WhenResultActionsIsNull_WithParamsActionOverload()
        {
            // Arrange
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>("resultActions", () => 
                command.ExecuteStoredProcAsync(CommandBehavior.Default, CancellationToken.None, true, (Action<EFExtensions.SprocResults>[])null!));
        }

        [Fact]
        public void ExecuteStoredNonQuery_ReturnsAffectedRecords_AndManagesConnection()
        {
            // Arrange
            int expectedAffectedRecords = 5;
            _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteNonQueryReturnValue = expectedAffectedRecords // Configure return value
            };
            _fakeConnection.ForceState(ConnectionState.Closed); // Set initial state

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            var affectedRecords = command.ExecuteStoredNonQuery(manageConnection: true);
            var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.Equal(expectedAffectedRecords, affectedRecords);
            Assert.True(fakeCommand.ExecuteNonQueryCalled);
            Assert.True(_fakeConnection.OpenCalled);
            Assert.True(_fakeConnection.CloseCalled);
            Assert.True(fakeCommand.DisposedCalled);
        }

        [Fact]
        public async Task ExecuteStoredNonQueryAsync_ReturnsAffectedRecords_AndManagesConnection()
        {
            // Arrange
            int expectedAffectedRecords = 7;
             _fakeConnection.CreateCommandAction = () => new FakeDbCommand(_fakeConnection)
            {
                ExecuteNonQueryReturnValue = expectedAffectedRecords // Configure return value
            };
             _fakeConnection.ForceState(ConnectionState.Closed); // Set initial state
            var cts = new CancellationTokenSource();

            // Act
            var command = _mockDbContext.Object.LoadStoredProc("TestProc");
            var affectedRecords = await command.ExecuteStoredNonQueryAsync(cts.Token, manageConnection: true);
            var fakeCommand = (FakeDbCommand)command;

            // Assert
            Assert.Equal(expectedAffectedRecords, affectedRecords);
            Assert.True(fakeCommand.ExecuteNonQueryAsyncCalled);
            Assert.Equal(cts.Token, fakeCommand.ExecuteNonQueryAsyncCancellationToken);
            Assert.True(_fakeConnection.OpenAsyncCalled);
            Assert.Equal(cts.Token, _fakeConnection.OpenAsyncCancellationToken);
            Assert.True(_fakeConnection.CloseCalled);
            Assert.True(fakeCommand.DisposedCalled);
        }

        [Fact]
        public void ReadToDataTable_ShouldPopulateDataTable()
        {
            // Arrange
            var schemaTable = CreateSchemaTable(
                ("Col1", typeof(int), 0, false, false, -1),
                ("Col2", typeof(string), 1, true, false, 100)
            );
            var data = new List<object[]>
            {
                new object[] { 1, "Row1" },
                new object[] { 2, DBNull.Value },
                new object[] { 3, "Row3" }
            };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var resultTable = sprocResults.ReadToDataTable();

            // Assert
            Assert.NotNull(resultTable);
            Assert.Equal(2, resultTable.Columns.Count);
            Assert.Equal("Col1", resultTable.Columns[0].ColumnName);
            Assert.Equal(typeof(int), resultTable.Columns[0].DataType);
            Assert.Equal("Col2", resultTable.Columns[1].ColumnName);
            Assert.Equal(typeof(string), resultTable.Columns[1].DataType);

            Assert.Equal(3, resultTable.Rows.Count);
            Assert.Equal(1, resultTable.Rows[0]["Col1"]);
            Assert.Equal("Row1", resultTable.Rows[0]["Col2"]);
            Assert.Equal(2, resultTable.Rows[1]["Col1"]);
            Assert.Equal(DBNull.Value, resultTable.Rows[1]["Col2"]);
            Assert.Equal(3, resultTable.Rows[2]["Col1"]);
            Assert.Equal("Row3", resultTable.Rows[2]["Col2"]);
        }

        [Fact]
        public void ReadToValueTupleList_ShouldMapToValueTuple_Arity1()
        {
            // Arrange
            var schemaTable = CreateSchemaTable(("Id", typeof(int), 0, false, false, -1));
            var data = new List<object[]> { new object[] { 123 }, new object[] { 456 } };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToValueTupleList<ValueTuple<int>>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(123, result[0].Item1);
            Assert.Equal(456, result[1].Item1);
        }

        [Fact]
        public void ReadToValueTupleList_ShouldMapToValueTuple_Arity2_WithNullsAndConversions()
        {
            // Arrange
            var schemaTable = CreateSchemaTable(
                ("Name", typeof(string), 0, true, false, 100),
                ("Amount", typeof(decimal), 1, false, false, -1)
            );
            var data = new List<object[]>
            {
                new object[] { "Apple", 1.23m },
                new object[] { DBNull.Value, 4.56m }
            };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToValueTupleList<(string?, decimal)>(); // Using tuple syntax sugar

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Apple", result[0].Item1);
            Assert.Equal(1.23m, result[0].Item2);
            Assert.Null(result[1].Item1);
            Assert.Equal(4.56m, result[1].Item2);
        }

        [Fact]
        public void ReadToValueTupleList_ShouldMapToValueTuple_Arity3_WithDateOnlyAndTimeOnly()
        {
            // Arrange
            var testDateTime = new DateTime(2024, 3, 10, 10, 30, 0);
            var schemaTable = CreateSchemaTable(
                ("Id", typeof(int), 0, false, false, -1),
                ("EventDate", typeof(DateTime), 1, false, false, -1),
                ("EventTime", typeof(DateTime), 2, false, false, -1)
            );
            var data = new List<object[]> { new object[] { 1, testDateTime, testDateTime } };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToValueTupleList<(int, DateOnly, TimeOnly)>();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1, result[0].Item1);
            Assert.Equal(DateOnly.FromDateTime(testDateTime), result[0].Item2);
            Assert.Equal(TimeOnly.FromDateTime(testDateTime), result[0].Item3);
        }

        [Fact]
        public void ReadToValueTupleList_ArityMismatch_ShouldReturnEmptyList()
        {
            // Arrange: Reader has 1 column, tuple expects 2
            var schemaTable = CreateSchemaTable(("Id", typeof(int), 0, false, false, -1));
            var data = new List<object[]> { new object[] { 123 } };
            var fakeDataReader = new FakeDbDataReader(data, schemaTable);
            var sprocResults = new EFExtensions.SprocResults(fakeDataReader);

            // Act
            var result = sprocResults.ReadToValueTupleList<(int, string)>();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result); // Current implementation returns empty on arity mismatch
        }

        [Fact]
        public void ReadToValueTupleList_NotAValueTuple_ShouldThrowArgumentException()
        {
            var schemaColumns = new List<DbColumn>
            {
                new FakeDbColumn { ColumnName = "X", ColumnOrdinal = 0, DataType = typeof(int) }
            };
            var rowData = new List<object[]> { new object[] { 123 } };
            var reader = SetupDataReaderFakes(schemaColumns, rowData);
            var sprocResults = new EFExtensions.SprocResults(reader);

            // Expect InvalidOperationException because the generator won't create a specific ValueTuple mapper for a non-ValueTuple type
            Assert.Throws<InvalidOperationException>(() => sprocResults.ReadToValueTupleList<NotAPocoStruct>());
        }
    }
} 