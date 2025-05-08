#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
#pragma warning disable CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).

using System.Data;
using System.Data.Common;

namespace Snickler.EFCore.Tests.Fakes
{
    // Basic implementation, needs refinement based on usage
    public class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string? SourceColumn { get; set; } = null;
        public override bool SourceColumnNullMapping { get; set; }
        private object? _valueBackingField;
        public override object? Value 
        {
            get 
            {
                return _valueBackingField;
            }
            set 
            {
                _valueBackingField = value;
            }
        }

        public override void ResetDbType() { }
    }
} 