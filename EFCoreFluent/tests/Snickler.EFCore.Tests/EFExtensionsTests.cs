using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data.Common;
using System.Data;
using Snickler.EFCore; // Your project's namespace
using Microsoft.EntityFrameworkCore.Metadata;

namespace Snickler.EFCore.Tests
{
    public class EFExtensionsTests
    {
        private readonly Mock<DbConnection> _mockConnection;
        private readonly Mock<DbCommand> _mockCommand;
        private readonly Mock<DatabaseFacade> _mockDatabaseFacade;
        private readonly Mock<DbContext> _mockDbContext;
        private readonly Mock<IModel> _mockModel;

        public EFExtensionsTests()
        {
            _mockConnection = new Mock<DbConnection>();
            _mockCommand = new Mock<DbCommand>();
            _mockDatabaseFacade = new Mock<DatabaseFacade>(Mock.Of<DbContext>()); // DatabaseFacade requires a DbContext
            _mockDbContext = new Mock<DbContext>(new DbContextOptions<DbContext>());
            _mockModel = new Mock<IModel>();

            // Setup Connection to return Command
            _mockConnection.Setup(c => c.CreateCommand()).Returns(_mockCommand.Object);

            // Setup DatabaseFacade to return Connection
            _mockDatabaseFacade.Setup(db => db.GetDbConnection()).Returns(_mockConnection.Object);

            // Setup DbContext to return DatabaseFacade and Model
            _mockDbContext.Setup(ctx => ctx.Database).Returns(_mockDatabaseFacade.Object);
            _mockDbContext.Setup(ctx => ctx.Model).Returns(_mockModel.Object);
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

        // TODO: Add tests for WithSqlParam
        // TODO: Add tests for MapToList (this will be more complex due to DbDataReader mocking)
        // TODO: Add tests for ExecuteStoredProc and variants
    }
} 