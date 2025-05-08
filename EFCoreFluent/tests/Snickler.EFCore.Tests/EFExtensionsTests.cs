using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data.Common;
using System.Data;
using Snickler.EFCore; // Your project's namespace
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema; // For ColumnAttribute
using System.Collections.ObjectModel; // For ReadOnlyCollection
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Snickler.EFCore.Tests
{
    // --- Test POCOs for MapToList --- 
    public class SimplePoco
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal? Value { get; set; }
    }

    public class PocoWithAttributes
    {
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("PRODUCT_NAME")] // Test case insensitivity of attribute mapping
        public string ProductName { get; set; }

        public string UnmappedProperty { get; set; } = "Default";
    }

    public class PocoWithDateAndTime
    {
        public int Id { get; set; }
        public DateOnly EventDate { get; set; }
        public TimeOnly EventTime { get; set; }
        public DateTime EventDateTime { get; set; }
    }

    public class EFExtensionsTests
    {
        private readonly Mock<DbConnection> _mockConnection;
        private readonly Mock<DbCommand> _mockCommand;
        private readonly Mock<DatabaseFacade> _mockDatabaseFacade;
        private readonly Mock<DbContext> _mockDbContext;
        private readonly Mock<IModel> _mockModel;
        private readonly Mock<DbParameterCollection> _mockParameterCollection;
        private readonly List<DbParameter> _parametersList; // To act as a backing store for the collection

        public EFExtensionsTests()
        {
            _mockConnection = new Mock<DbConnection>();
            _mockCommand = new Mock<DbCommand>();
            _mockDatabaseFacade = new Mock<DatabaseFacade>(Mock.Of<DbContext>());
            _mockDbContext = new Mock<DbContext>(new DbContextOptions<DbContext>());
            _mockModel = new Mock<IModel>();
            _mockParameterCollection = new Mock<DbParameterCollection>();
            _parametersList = new List<DbParameter>();

            _mockConnection.Setup(c => c.CreateCommand()).Returns(_mockCommand.Object);
            _mockDatabaseFacade.Setup(db => db.GetDbConnection()).Returns(_mockConnection.Object);
            _mockDbContext.Setup(ctx => ctx.Database).Returns(_mockDatabaseFacade.Object);
            _mockDbContext.Setup(ctx => ctx.Model).Returns(_mockModel.Object);

            // Setup for command parameters
            _mockCommand.Setup(c => c.Parameters).Returns(_mockParameterCollection.Object);
            _mockCommand.Setup(c => c.CreateParameter()).Returns(() => new Mock<DbParameter>().Object);

            // Mock ParameterCollection Add/AddRange to use our list
            _mockParameterCollection.Setup(p => p.Add(It.IsAny<DbParameter>()))
                .Callback<DbParameter>(param => _parametersList.Add(param));
            _mockParameterCollection.Setup(p => p.AddRange(It.IsAny<DbParameter[]>()))
                .Callback<DbParameter[]>(paramArray => _parametersList.AddRange(paramArray));

            // Set CommandText and CommandType by default for most parameter tests
            _mockCommand.Object.CommandText = "TestProc";
            _mockCommand.Object.CommandType = CommandType.StoredProcedure;
        }

        [Fact]
        public void LoadStoredProc_ShouldSetCommandTextAndType()
        {
            // Arrange
            var storedProcName = "TestProc";
            _mockModel.Setup(m => m.GetDefaultSchema()).Returns((string)null); // No default schema

            // Act
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: false);

            // Assert
            _mockCommand.VerifySet(c => c.CommandText = storedProcName, Times.Once);
            _mockCommand.VerifySet(c => c.CommandType = CommandType.StoredProcedure, Times.Once);
            Assert.Equal(30, command.CommandTimeout); // Default timeout
        }

        [Fact]
        public void LoadStoredProc_ShouldPrependDefaultSchema_WhenSchemaExists()
        {
            // Arrange
            var storedProcName = "TestProc";
            var schemaName = "dbo";
            var expectedCommandText = $"{schemaName}.{storedProcName}";
            _mockModel.Setup(m => m.GetDefaultSchema()).Returns(schemaName);

            // Act
            _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: true);

            // Assert
            _mockCommand.VerifySet(c => c.CommandText = expectedCommandText, Times.Once);
        }

        [Fact]
        public void LoadStoredProc_ShouldNotPrependDefaultSchema_WhenSchemaDoesNotExist()
        {
            // Arrange
            var storedProcName = "TestProc";
            _mockModel.Setup(m => m.GetDefaultSchema()).Returns((string)null);

            // Act
            _mockDbContext.Object.LoadStoredProc(storedProcName, prependDefaultSchema: true);

            // Assert
            _mockCommand.VerifySet(c => c.CommandText = storedProcName, Times.Once);
        }

        [Fact]
        public void LoadStoredProc_ShouldSetCustomCommandTimeout()
        {
            // Arrange
            var storedProcName = "TestProc";
            short customTimeout = 60;
            _mockModel.Setup(m => m.GetDefaultSchema()).Returns((string)null);

            // Act
            var command = _mockDbContext.Object.LoadStoredProc(storedProcName, commandTimeout: customTimeout);

            // Assert
            Assert.Equal(customTimeout, command.CommandTimeout);
            _mockCommand.VerifySet(c => c.CommandTimeout = customTimeout, Times.Once);
        }

        // TODO: Add tests for MapToList (this will be more complex due to DbDataReader mocking)
        // TODO: Add tests for ExecuteStoredProc and variants

        // --- Tests for WithSqlParam --- 

        [Fact]
        public void WithSqlParam_WithValue_ShouldAddParameter()
        {
            // Arrange
            var paramName = "@TestParam";
            var paramValue = "TestValue";
            var mockDbParameter = new Mock<DbParameter>();
            _mockCommand.Setup(c => c.CreateParameter()).Returns(mockDbParameter.Object);

            // Act
            _mockCommand.Object.WithSqlParam(paramName, paramValue);

            // Assert
            mockDbParameter.VerifySet(p => p.ParameterName = paramName, Times.Once);
            mockDbParameter.VerifySet(p => p.Value = paramValue, Times.Once);
            Assert.Contains(mockDbParameter.Object, _parametersList);
        }

        [Fact]
        public void WithSqlParam_WithNullValue_ShouldAddParameterWithDBNull()
        {
            // Arrange
            var paramName = "@TestParam";
            var mockDbParameter = new Mock<DbParameter>();
            _mockCommand.Setup(c => c.CreateParameter()).Returns(mockDbParameter.Object);

            // Act
            _mockCommand.Object.WithSqlParam(paramName, null);

            // Assert
            mockDbParameter.VerifySet(p => p.ParameterName = paramName, Times.Once);
            mockDbParameter.VerifySet(p => p.Value = DBNull.Value, Times.Once);
            Assert.Contains(mockDbParameter.Object, _parametersList);
        }

        [Fact]
        public void WithSqlParam_WithConfigureAction_ShouldInvokeAction()
        {
            // Arrange
            var paramName = "@TestParam";
            var paramValue = 123;
            var mockDbParameter = new Mock<DbParameter>();
            _mockCommand.Setup(c => c.CreateParameter()).Returns(mockDbParameter.Object);
            bool configureActionCalled = false;
            Action<DbParameter> configureParam = p => 
            {
                p.DbType = DbType.Int32;
                configureActionCalled = true; 
            };

            // Act
            _mockCommand.Object.WithSqlParam(paramName, paramValue, configureParam);

            // Assert
            Assert.True(configureActionCalled);
            mockDbParameter.VerifySet(p => p.DbType = DbType.Int32, Times.Once);
            Assert.Contains(mockDbParameter.Object, _parametersList);
        }

        [Fact]
        public void WithSqlParam_OverloadWithoutValue_ShouldAddParameterAndInvokeAction()
        {
            // Arrange
            var paramName = "@OutputParam";
            var mockDbParameter = new Mock<DbParameter>();
            _mockCommand.Setup(c => c.CreateParameter()).Returns(mockDbParameter.Object);
            bool configureActionCalled = false;
            Action<DbParameter> configureParam = p => 
            { 
                p.Direction = ParameterDirection.Output;
                configureActionCalled = true; 
            };

            // Act
            _mockCommand.Object.WithSqlParam(paramName, configureParam);

            // Assert
            mockDbParameter.VerifySet(p => p.ParameterName = paramName, Times.Once);
            Assert.True(configureActionCalled);
            mockDbParameter.VerifySet(p => p.Direction = ParameterDirection.Output, Times.Once);
            Assert.Contains(mockDbParameter.Object, _parametersList);
        }

        [Fact]
        public void WithSqlParam_WithIDbDataParameter_ShouldAddParameter()
        {
            // Arrange
            var mockIDbDataParameter = new Mock<IDbDataParameter>();
            mockIDbDataParameter.Setup(p => p.ParameterName).Returns("@CustomParam");

            // Act
            _mockCommand.Object.WithSqlParam(mockIDbDataParameter.Object);

            // Assert
            Assert.Contains(mockIDbDataParameter.Object, _parametersList);
        }

        [Fact]
        public void WithSqlParams_WithParameterArray_ShouldAddAllParameters()
        {
            // Arrange
            var param1 = new Mock<IDbDataParameter>();
            var param2 = new Mock<IDbDataParameter>();
            var parameters = new[] { param1.Object, param2.Object };

            // Act
            _mockCommand.Object.WithSqlParams(parameters);

            // Assert
            Assert.Contains(param1.Object, _parametersList);
            Assert.Contains(param2.Object, _parametersList);
            _mockParameterCollection.Verify(pc => pc.AddRange(parameters), Times.Once);
        }

        [Theory]
        [InlineData(null, CommandType.Text)]          // Null CommandText
        [InlineData("SELECT 1", CommandType.Text)]   // Not StoredProcedure type
        [InlineData("", CommandType.StoredProcedure)] // Empty CommandText
        public void WithSqlParam_ThrowsInvalidOperation_WhenCommandNotReady(string commandText, CommandType commandType)
        {
            // Arrange
            // Reset to non-SP type for these specific tests, constructor sets it up for valid SP calls
            _mockCommand.Object.CommandText = commandText;
            _mockCommand.Object.CommandType = commandType;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _mockCommand.Object.WithSqlParam("@Param", 123));
            Assert.Throws<InvalidOperationException>(() => _mockCommand.Object.WithSqlParam("@Param", p => { }));
            Assert.Throws<InvalidOperationException>(() => _mockCommand.Object.WithSqlParam(new Mock<IDbDataParameter>().Object));
            Assert.Throws<InvalidOperationException>(() => _mockCommand.Object.WithSqlParams(new IDbDataParameter[] { new Mock<IDbDataParameter>().Object }));
        
            // Reset command for other tests if necessary, though each test method should be isolated.
            // For safety, explicitly set it back if other tests rely on the constructor's default.
            _mockCommand.Object.CommandText = "TestProc"; // Default valid state
            _mockCommand.Object.CommandType = CommandType.StoredProcedure; // Default valid state
        }

        // --- Tests for MapToList --- 

        private Mock<DbDataReader> SetupDataReaderMocks(List<DbColumn> schemaColumns, List<object[]>rowData, bool hasRows = true)
        {
            var mockDataReader = new Mock<DbDataReader>();

            mockDataReader.Setup(r => r.HasRows).Returns(hasRows && rowData != null && rowData.Count > 0);

            // Setup GetColumnSchema
            var readOnlySchemaColumns = new ReadOnlyCollection<DbColumn>(schemaColumns);
            mockDataReader.Setup(r => r.GetColumnSchema()).Returns(readOnlySchemaColumns);

            // Setup Read() to iterate through rowData
            var readSequence = mockDataReader.SetupSequence(r => r.Read());
            if (rowData != null)
            {
                foreach (var _ in rowData)
                {
                    readSequence.Returns(true);
                }
            }
            readSequence.Returns(false);

            // Setup GetValue to return data from the current row
            int currentRow = -1;
            mockDataReader.Setup(r => r.GetValue(It.IsAny<int>()))
                .Callback(() => {
                    // This callback helps align GetValue with the current Read() state if Read() is called multiple times per row, 
                    // but typical usage is one Read() then multiple GetValue(ordinal) for that row.
                    // The actual data serving logic is below.
                })
                .Returns<int>(ordinal => 
                {
                    // This logic assumes Read() has been called to advance to a valid row.
                    // If Read() was the most recent call advancing currentRow, this will be correct.
                    if (currentRow < 0 || currentRow >= rowData.Count) return DBNull.Value; // Should not happen if Read() controls flow
                    if (ordinal < 0 || ordinal >= rowData[currentRow].Length) return DBNull.Value;
                    return rowData[currentRow][ordinal] ?? DBNull.Value;
                });
            
            // Link Read() call to advancing the current row index for GetValue
            // This is a simplified way; for complex scenarios, manage state more explicitly.
            mockDataReader.Setup(r => r.Read()).Callback(() => currentRow++).Returns(() => currentRow < rowData.Count);
            // Re-setup the sequence with the callback logic embedded
            var finalReadSequence = mockDataReader.SetupSequence(r => r.Read());
            if (rowData != null)
            {
                for(int i = 0; i < rowData.Count; i++)
                {
                    finalReadSequence.Returns(true);
                }
            }
            finalReadSequence.Returns(false);
            
            // Reset currentRow for each test sequence starting
            currentRow = -1; 
            mockDataReader.Setup(r => r.Read())
                .Callback(() => currentRow++)
                .Returns(() => currentRow < (rowData?.Count ?? 0));

            return mockDataReader;
        }

        [Fact]
        public void MapToList_SimplePoco_ShouldMapCorrectly()
        {
            // Arrange
            var schema = new List<DbColumn>
            {
                Mock.Of<DbColumn>(c => c.ColumnName == "Id" && c.ColumnOrdinal == 0),
                Mock.Of<DbColumn>(c => c.ColumnName == "Name" && c.ColumnOrdinal == 1),
                Mock.Of<DbColumn>(c => c.ColumnName == "Value" && c.ColumnOrdinal == 2)
            };
            var data = new List<object[]>
            {
                new object[] { 1, "Test1", 10.5m },
                new object[] { 2, "Test2", DBNull.Value }, // Test nullable decimal
                new object[] { 3, "Test3", 20.0m }
            };

            var mockDataReader = SetupDataReaderMocks(schema, data);
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

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
            var schema = new List<DbColumn>
            {
                Mock.Of<DbColumn>(c => c.ColumnName == "product_id" && c.ColumnOrdinal == 0),
                Mock.Of<DbColumn>(c => c.ColumnName == "product_name" && c.ColumnOrdinal == 1), // Deliberately lowercase to test case-insensitivity of mapping
                Mock.Of<DbColumn>(c => c.ColumnName == "ExtraColumn" && c.ColumnOrdinal == 2) // This column won't be mapped
            };
            var data = new List<object[]>
            {
                new object[] { 101, "Laptop", "SomeExtraValue" }
            };

            var mockDataReader = SetupDataReaderMocks(schema, data);
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToList<PocoWithAttributes>();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(101, result[0].ProductId);
            Assert.Equal("Laptop", result[0].ProductName);
            Assert.Equal("Default", result[0].UnmappedProperty); // Should retain default value
        }
        
        [Fact]
        public void MapToList_PocoWithDateAndTime_ShouldMapCorrectly()
        {
            // Arrange
            var testDate = new DateTime(2023, 10, 26, 14, 30, 15);
            var schema = new List<DbColumn>
            {
                Mock.Of<DbColumn>(c => c.ColumnName == "Id" && c.ColumnOrdinal == 0),
                Mock.Of<DbColumn>(c => c.ColumnName == "EventDate" && c.ColumnOrdinal == 1 && c.DataType == typeof(DateTime) && c.DataTypeName == "datetime"),
                Mock.Of<DbColumn>(c => c.ColumnName == "EventTime" && c.ColumnOrdinal == 2 && c.DataType == typeof(DateTime) && c.DataTypeName == "datetime"),
                Mock.Of<DbColumn>(c => c.ColumnName == "EventDateTime" && c.ColumnOrdinal == 3 && c.DataType == typeof(DateTime) && c.DataTypeName == "datetime"),
            };
            var data = new List<object[]>
            {
                new object[] { 1, testDate, testDate, testDate }
            };

            var mockDataReader = SetupDataReaderMocks(schema, data);
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);
            
            // Act
            var result = sprocResults.ReadToList<PocoWithDateAndTime>();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
            Assert.Equal(DateOnly.FromDateTime(testDate), result[0].EventDate);
            Assert.Equal(TimeOnly.FromDateTime(testDate), result[0].EventTime);
            Assert.Equal(testDate, result[0].EventDateTime); // Direct DateTime mapping
        }

        [Fact]
        public void MapToList_EmptyResultSet_ShouldReturnEmptyList()
        {
            // Arrange
            var schema = new List<DbColumn>
            { // Schema still defined, but no data
                Mock.Of<DbColumn>(c => c.ColumnName == "Id" && c.ColumnOrdinal == 0)
            };
            var data = new List<object[]>(); // Empty data

            var mockDataReader = SetupDataReaderMocks(schema, data, hasRows: false);
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

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
            var schema = new List<DbColumn>(); // No schema, no data implies no rows
            var data = new List<object[]>();

            var mockDataReader = new Mock<DbDataReader>();
            mockDataReader.Setup(r => r.HasRows).Returns(false);
            // No need to setup GetColumnSchema or Read/GetValue if HasRows is false, as MapToList checks HasRows first.
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToList<SimplePoco>();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // TODO: Add tests for ExecuteStoredProc and variants
        // TODO: Add tests for MapToValue<T>

        // --- Tests for MapToValue<T> ---

        [Fact]
        public void MapToValue_Int_ShouldReturnValue()
        {
            // Arrange
            var schema = new List<DbColumn> { Mock.Of<DbColumn>(c => c.ColumnName == "Value" && c.ColumnOrdinal == 0 && c.DataType == typeof(int)) };
            var data = new List<object[]> { new object[] { 123 } };
            var mockDataReader = SetupDataReaderMocks(schema, data);
            // We need to ensure GetFieldValue<T> is correctly mocked for the specific type T
            mockDataReader.Setup(r => r.GetFieldValue<int>(0)).Returns(123);
            mockDataReader.Setup(r => r.IsDBNull(0)).Returns(false);

            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToValue<int>();

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal(123, result.Value);
        }

        [Fact]
        public void MapToValue_Guid_ShouldReturnValue()
        {
            // Arrange
            var testGuid = Guid.NewGuid();
            var schema = new List<DbColumn> { Mock.Of<DbColumn>(c => c.ColumnName == "Value" && c.ColumnOrdinal == 0 && c.DataType == typeof(Guid)) };
            var data = new List<object[]> { new object[] { testGuid } };
            var mockDataReader = SetupDataReaderMocks(schema, data);
            mockDataReader.Setup(r => r.GetFieldValue<Guid>(0)).Returns(testGuid);
            mockDataReader.Setup(r => r.IsDBNull(0)).Returns(false);

            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToValue<Guid>();

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal(testGuid, result.Value);
        }

        [Fact]
        public void MapToValue_ValueIsDBNull_ShouldReturnNull()
        {
            // Arrange
            var schema = new List<DbColumn> { Mock.Of<DbColumn>(c => c.ColumnName == "Value" && c.ColumnOrdinal == 0 && c.DataType == typeof(int)) };
            var data = new List<object[]> { new object[] { DBNull.Value } }; // Data row exists, but value is DBNull
            var mockDataReader = SetupDataReaderMocks(schema, data);
            mockDataReader.Setup(r => r.IsDBNull(0)).Returns(true);
            // GetFieldValue<T> won't be called if IsDBNull is true for that ordinal

            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToValue<int>();

            // Assert
            Assert.False(result.HasValue);
        }

        [Fact]
        public void MapToValue_ReaderHasNoRows_ShouldReturnNull()
        {
            // Arrange
            var mockDataReader = new Mock<DbDataReader>();
            mockDataReader.Setup(r => r.HasRows).Returns(false);
            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToValue<int>();

            // Assert
            Assert.False(result.HasValue);
        }

        [Fact]
        public void MapToValue_ReaderHasRowsButReadReturnsFalse_ShouldReturnNull()
        {
            // Arrange
            var schema = new List<DbColumn> { Mock.Of<DbColumn>(c => c.ColumnName == "Value" && c.ColumnOrdinal == 0) };
            // SetupDataReaderMocks with empty data will make HasRows true (if schema provided) but Read() will return false.
            var mockDataReader = SetupDataReaderMocks(schema, new List<object[]>(), hasRows: true); 
            // Ensure Read() is indeed false if called despite HasRows being potentially true
            mockDataReader.Setup(r => r.Read()).Returns(false);

            var sprocResults = new EFExtensions.SprocResults(mockDataReader.Object);

            // Act
            var result = sprocResults.ReadToValue<int>();

            // Assert
            Assert.False(result.HasValue);
        }

        // TODO: Add tests for ExecuteStoredProc and variants

        // --- Tests for ExecuteStoredProc ---

        [Fact]
        public void ExecuteStoredProc_CallsHandleResults_AndManagesConnection()
        {
            // Arrange
            var mockDataReader = new Mock<DbDataReader>();
            _mockCommand.Setup(c => c.ExecuteReader(CommandBehavior.Default)).Returns(mockDataReader.Object);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = results => 
            {
                Assert.NotNull(results);
                handleResultsCalled = true; 
            };

            // Act
            _mockCommand.Object.ExecuteStoredProc(handleResults, CommandBehavior.Default, manageConnection: true);

            // Assert
            Assert.True(handleResultsCalled);
            _mockCommand.Verify(c => c.ExecuteReader(CommandBehavior.Default), Times.Once);
            _mockConnection.Verify(c => c.Open(), Times.Once); // Connection was closed, so opened
            _mockConnection.Verify(c => c.Close(), Times.Once); // Connection was managed, so closed
            _mockCommand.Verify(c => c.Dispose(), Times.Once); // Command is disposed
        }

        [Fact]
        public void ExecuteStoredProc_DoesNotManageConnection_WhenManageConnectionIsFalse()
        {
            // Arrange
            var mockDataReader = new Mock<DbDataReader>();
            _mockCommand.Setup(c => c.ExecuteReader(CommandBehavior.Default)).Returns(mockDataReader.Object);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Open); // Assume connection is already open
            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = r => { handleResultsCalled = true; };

            // Act
            _mockCommand.Object.ExecuteStoredProc(handleResults, CommandBehavior.Default, manageConnection: false);

            // Assert
            Assert.True(handleResultsCalled);
            _mockCommand.Verify(c => c.ExecuteReader(CommandBehavior.Default), Times.Once);
            _mockConnection.Verify(c => c.Open(), Times.Never); // Connection not managed or opened by method
            _mockConnection.Verify(c => c.Close(), Times.Never); // Connection not managed or closed by method
            _mockCommand.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void ExecuteStoredProc_ThrowsArgumentNullException_WhenHandleResultsIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>("handleResults", () => 
                _mockCommand.Object.ExecuteStoredProc(null, CommandBehavior.Default, true));
        }

        // --- Tests for ExecuteStoredProcAsync ---
        [Fact]
        public async Task ExecuteStoredProcAsync_CallsHandleResults_AndManagesConnection_WithAction()
        {
            // Arrange
            var mockDataReader = new Mock<DbDataReader>();
            var cts = new CancellationTokenSource();
            _mockCommand.Setup(c => c.ExecuteReaderAsync(CommandBehavior.Default, cts.Token))
                        .ReturnsAsync(mockDataReader.Object);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            bool handleResultsCalled = false;
            Action<EFExtensions.SprocResults> handleResults = results => { 
                Assert.NotNull(results);
                handleResultsCalled = true; 
            };

            // Act
            await _mockCommand.Object.ExecuteStoredProcAsync(handleResults, CommandBehavior.Default, cts.Token, manageConnection: true);

            // Assert
            Assert.True(handleResultsCalled);
            _mockCommand.Verify(c => c.ExecuteReaderAsync(CommandBehavior.Default, cts.Token), Times.Once);
            _mockConnection.Verify(c => c.OpenAsync(cts.Token), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
            _mockCommand.Verify(c => c.Dispose(), Times.Once);
        }
        
        [Fact]
        public async Task ExecuteStoredProcAsync_CallsResultActions_AndManagesConnection_WithParamsAction()
        {
            // Arrange
            var mockDataReader = new Mock<DbDataReader>();
            var cts = new CancellationTokenSource();
            _mockCommand.Setup(c => c.ExecuteReaderAsync(CommandBehavior.Default, cts.Token))
                        .ReturnsAsync(mockDataReader.Object);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            int action1Called = 0;
            int action2Called = 0;
            Action<EFExtensions.SprocResults> action1 = results => { action1Called++; }; 
            Action<EFExtensions.SprocResults> action2 = results => { action2Called++; };

            // Act
            await _mockCommand.Object.ExecuteStoredProcAsync(CommandBehavior.Default, cts.Token, true, action1, action2);

            // Assert
            Assert.Equal(1, action1Called);
            Assert.Equal(1, action2Called);
            _mockCommand.Verify(c => c.ExecuteReaderAsync(CommandBehavior.Default, cts.Token), Times.Once);
            _mockConnection.Verify(c => c.OpenAsync(cts.Token), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
            _mockCommand.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ExecuteStoredProcAsync_ThrowsArgumentNullException_WhenHandleResultsIsNull_WithActionOverload()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>("handleResults", () => 
                _mockCommand.Object.ExecuteStoredProcAsync((Action<EFExtensions.SprocResults>)null, CommandBehavior.Default, CancellationToken.None, true));
        }

        [Fact]
        public async Task ExecuteStoredProcAsync_ThrowsArgumentNullException_WhenResultActionsIsNull_WithParamsActionOverload()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>("resultActions", () => 
                _mockCommand.Object.ExecuteStoredProcAsync(CommandBehavior.Default, CancellationToken.None, true, (Action<EFExtensions.SprocResults>[])null));
        }

        // --- Tests for ExecuteStoredNonQuery ---
        [Fact]
        public void ExecuteStoredNonQuery_ReturnsAffectedRecords_AndManagesConnection()
        {
            // Arrange
            int expectedAffectedRecords = 5;
            _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(expectedAffectedRecords);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);

            // Act
            var affectedRecords = _mockCommand.Object.ExecuteStoredNonQuery(manageConnection: true);

            // Assert
            Assert.Equal(expectedAffectedRecords, affectedRecords);
            _mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once);
            _mockConnection.Verify(c => c.Open(), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
            _mockCommand.Verify(c => c.Dispose(), Times.Once);
        }

        // --- Tests for ExecuteStoredNonQueryAsync ---
        [Fact]
        public async Task ExecuteStoredNonQueryAsync_ReturnsAffectedRecords_AndManagesConnection()
        {
            // Arrange
            int expectedAffectedRecords = 7;
            var cts = new CancellationTokenSource();
            _mockCommand.Setup(c => c.ExecuteNonQueryAsync(cts.Token)).ReturnsAsync(expectedAffectedRecords);
            _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);

            // Act
            var affectedRecords = await _mockCommand.Object.ExecuteStoredNonQueryAsync(cts.Token, manageConnection: true);

            // Assert
            Assert.Equal(expectedAffectedRecords, affectedRecords);
            _mockCommand.Verify(c => c.ExecuteNonQueryAsync(cts.Token), Times.Once);
            _mockConnection.Verify(c => c.OpenAsync(cts.Token), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
            _mockCommand.Verify(c => c.Dispose(), Times.Once);
        }
    }
} 