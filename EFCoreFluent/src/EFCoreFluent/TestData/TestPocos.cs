#nullable enable

using System;
using System.ComponentModel.DataAnnotations.Schema; // For ColumnAttribute

namespace Snickler.EFCore.TestData // Use a specific namespace
{
    // --- Test POCOs defined in the main library for generator access ---
    internal class SimplePoco
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal? Value { get; set; }
    }

    internal class PocoWithAttributes
    {
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("PRODUCT_NAME")]
        public string? ProductName { get; set; }

        public string UnmappedProperty { get; set; } = "Default";
    }

    internal class PocoWithDateAndTime
    {
        public int Id { get; set; }
        public DateOnly EventDate { get; set; }
        public TimeOnly EventTime { get; set; }
        public DateTime EventDateTime { get; set; }
    }

    internal class PocoTypeForEmptySet 
    { 
        public int A { get; set; } 
    }
    
    internal class PocoTypeForNoRows 
    { 
        public string? B { get; set; } // Made nullable during previous warning fix
    }

    // Struct defined here for consistency, used in generator trigger + test
    internal struct NotAPocoStruct { public int X { get; set; } }
} 