using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq; // Required for Linq extension methods
using System.Threading;
using System.Threading.Tasks;

namespace Snickler.EFCore.Tests.Fakes
{
    // Basic implementation, needs refinement based on usage
    public class FakeDbDataReader : DbDataReader
    {
        private readonly List<object[]>? _data;
        private readonly DataTable? _schemaTable;
        private int _currentRow = -1;
        private bool _isClosed = false;
        private bool _hasRows;

        // Store column ordinals for faster lookup
        private readonly Dictionary<string, int>? _ordinalLookup;

        public FakeDbDataReader(List<object[]>? data, DataTable? schemaTable)
        {
            _data = data ?? new List<object[]>();
            _schemaTable = schemaTable;
            _hasRows = _data.Count > 0;

            // Pre-calculate ordinals if schema is available
            if (_schemaTable != null)
            {
                _ordinalLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _schemaTable.Rows.Count; i++)
                {
                    var colName = _schemaTable.Rows[i]["ColumnName"] as string;
                    if (!string.IsNullOrEmpty(colName) && !_ordinalLookup.ContainsKey(colName))
                    {
                        _ordinalLookup.Add(colName, i);
                    }
                }
            }
        }

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override int Depth => 0;
        public override bool IsClosed => _isClosed;
        public override int RecordsAffected => 0; // Not applicable for queries

        public override bool HasRows => _hasRows;

        public override int FieldCount => _schemaTable?.Rows.Count ?? (_data?.Count > 0 ? _data[0].Length : 0);

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal)?.Name ?? string.Empty;
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override Type GetFieldType(int ordinal) => _schemaTable?.Rows[ordinal]?["DataType"] as Type ?? typeof(object);
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
        public override Guid GetGuid(int ordinal) => GetValue(ordinal) is Guid guid ? guid : Guid.Parse(GetValue(ordinal).ToString() ?? string.Empty);
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override string GetName(int ordinal) => _schemaTable?.Rows[ordinal]?["ColumnName"] as string ?? throw new IndexOutOfRangeException();

        public override int GetOrdinal(string name)
        {
            if (_ordinalLookup != null && _ordinalLookup.TryGetValue(name, out int ordinal))
            {
                return ordinal;
            }
            // Fallback if schema or lookup wasn't built
            if (_schemaTable != null)
            {
                 for (int i = 0; i < _schemaTable.Rows.Count; i++)
                 {
                    if (string.Equals(_schemaTable.Rows[i]["ColumnName"] as string, name, StringComparison.OrdinalIgnoreCase))
                    {
                         return i;
                    }
                 }
            }
            throw new IndexOutOfRangeException($"Column not found: {name}");
        }

        public override string GetString(int ordinal)
        {
             object value = GetValue(ordinal);
             return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }

        public override object GetValue(int ordinal)
        {
            if (IsClosed) throw new InvalidOperationException("Reader is closed.");
            if (_data == null || _currentRow < 0 || _currentRow >= _data.Count)
                throw new InvalidOperationException("No data available or Read() not called.");
            if (ordinal < 0 || ordinal >= _data[_currentRow].Length)
                throw new IndexOutOfRangeException();
            return _data[_currentRow][ordinal] ?? DBNull.Value;
        }

        public override int GetValues(object[] values)
        {
             if (IsClosed) throw new InvalidOperationException("Reader is closed.");
             if (_data == null || _currentRow < 0 || _currentRow >= _data.Count) return 0;
             int count = Math.Min(values.Length, _data[_currentRow].Length);
             for (int i = 0; i < count; i++)
             {
                 values[i] = _data[_currentRow][i] ?? DBNull.Value;
             }
             return count;
        }

        public override bool IsDBNull(int ordinal)
        {
            if (IsClosed) throw new InvalidOperationException("Reader is closed.");
            if (_data == null || _currentRow < 0 || _currentRow >= _data.Count) return true;
            if (ordinal < 0 || ordinal >= _data[_currentRow].Length) return true;
            return _data[_currentRow][ordinal] == null || _data[_currentRow][ordinal] == DBNull.Value;
        }

        public override bool NextResult()
        {
            // Basic: Assume only one result set
            return false;
        }
         public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        {
            // Basic: Assume only one result set
            return Task.FromResult(false);
        }

        public override bool Read()
        {
             if (IsClosed) throw new InvalidOperationException("Reader is closed.");
            if (_data == null || _currentRow >= _data.Count - 1)
            {
                _hasRows = false; // Mark no more rows
                return false;
            }
            _currentRow++;
            return true;
        }
        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            await Task.Yield(); // Simulate async
            return Read();
        }

        public override DataTable? GetSchemaTable()
        {
            if (IsClosed) throw new InvalidOperationException("Reader is closed.");
            return _schemaTable;
        }

        public override void Close()
        {
            _isClosed = true;
        }
        
        public override IEnumerator GetEnumerator()
        {
            // Simple implementation: allows foreach over the rows
            return new DbEnumerator(this, closeReader: false);
        }
    }
} 