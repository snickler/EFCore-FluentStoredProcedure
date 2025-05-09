#nullable enable

using System;
using System.Data.Common;

namespace Snickler.EFCore.Tests.Fakes
{
    // Simple implementation of DbColumn for testing purposes
    public class FakeDbColumn : DbColumn
    {
        // Use 'new' to hide base properties and provide settable ones
        // Only implementing properties known to be used by tests
        public new string? ColumnName { get; set; } 
        public new int? ColumnOrdinal { get; set; }
        public new Type? DataType { get; set; } 
        public new bool? AllowDBNull { get; set; }
        public new int? ColumnSize { get; set; }
        public new bool? IsKey { get; set; }

        // Provide a default constructor
        public FakeDbColumn() { }
    }
} 