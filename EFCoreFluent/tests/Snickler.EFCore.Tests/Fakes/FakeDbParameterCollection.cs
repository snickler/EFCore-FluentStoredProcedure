using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq; // Added for Linq extension methods

namespace Snickler.EFCore.Tests.Fakes
{
    // Basic implementation, needs refinement based on usage
    public class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new List<DbParameter>();

        public override int Count => _parameters.Count;
        public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

        public override int Add(object? value)
        {
            if (value is DbParameter parameter)
            {
                _parameters.Add(parameter);
                return _parameters.Count - 1;
            }
            throw new ArgumentException("Value must be a DbParameter", nameof(value));
        }

        public override void AddRange(Array values)
        {
            foreach (var item in values)
            {
                Add(item);
            }
        }

        public override void Clear() => _parameters.Clear();
        public override bool Contains(object value) => value is DbParameter param && _parameters.Contains(param);
        public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);
        public override int IndexOf(object value) => value is DbParameter param ? _parameters.IndexOf(param) : -1;
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object? value)
        { 
             if (value is DbParameter parameter)
             {
                _parameters.Insert(index, parameter);
             }
             else
             {
                 throw new ArgumentException("Value must be a DbParameter", nameof(value));
             }
        }
        public override void Remove(object value) 
        { 
            if (value is DbParameter parameter)
            {
                _parameters.Remove(parameter);
            }
        }
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _parameters.RemoveAll(p => p.ParameterName == parameterName);
        protected override DbParameter GetParameter(int index) 
        {
             var param = _parameters[index];
             return param;
        }
        protected override DbParameter GetParameter(string parameterName) 
        {
             var param = _parameters.FirstOrDefault(p => p.ParameterName == parameterName);
             if (param == null) 
                 throw new IndexOutOfRangeException($"Parameter '{parameterName}' not found.");
             return param;
        }
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) 
        {
            int index = IndexOf(parameterName);
            if (index >= 0) 
                _parameters[index] = value; 
            else 
                Add(value); // Or throw? Add seems more common.
        }
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        
        // Implementation for abstract member
        public override void CopyTo(Array array, int index)
        {            
            ((IList)_parameters).CopyTo(array, index);
        }
    }
} 