# Snickler.EFCore
Fluent Methods for mapping Stored Procedure results to objects in EntityFrameworkCore



[![NuGet](https://img.shields.io/nuget/v/Snickler.EFCore.svg)](https://www.nuget.org/packages/Snickler.EFCore)


## Usage

### Executing A Stored Procedure

Add the `using` statement to pull in the extension method. E.g: `using Snickler.EFCore`

```csharp

      var dbContext = GetDbContext();
      dbContext.LoadStoredProc("dbo.SomeSproc")
               .WithSqlParam("fooId", 1)              
               .ExecuteStoredProc((handler) =>
                {                  
                    var fooResults = handler.ReadToList<FooDto>();      
                    // do something with your results.
                });

```

### Handling Multiple Result Sets

```csharp

      var dbContext = GetDbContext();
      dbContext.LoadStoredProc("dbo.SomeSproc")
               .WithSqlParam("fooId", 1)              
               .ExecuteStoredProc((handler) =>
                {                  
                    var fooResults = handler.ReadToList<FooDto>();      
                    handler.NextResult();
                    var barResults = handler.ReadToList<BarDto>();
                    handler.NextResult();
                    var bazResults = handler.ReadToList<BazDto>()
                });

```

### Handling Output Parameters

```csharp

      DbParameter outputParam = null;
    
      var dbContext = GetDbContext();
      dbContext.LoadStoredProc("dbo.SomeSproc")
               .WithSqlParam("fooId", 1)  
               .WithSqlParam("myOutputParam", (dbParam) =>
               {                 
                 dbParam.Direction = System.Data.ParameterDirection.Output;
                 dbParam.DbType = System.Data.DbType.Int32;          
                 outputParam = dbParam;
               })
               .ExecuteStoredProc((handler) =>
                {                  
                    var fooResults = handler.ReadToList<FooDto>();      
                    handler.NextResult();
                    var barResults = handler.ReadToList<BarDto>();
                    handler.NextResult();
                    var bazResults = handler.ReadToList<BazDto>()
                });
                
                int outputParamValue = (int)outputParam?.Value;

```

### Using output parameters without returning a result set

```csharp

      DbParameter outputParam = null;

      var dbContext = GetDbContext();

      await dbContext.LoadStoredProc("dbo.SomeSproc")
            .WithSqlParam("InputParam1", 1)
            .WithSqlParam("myOutputParam", (dbParam) =>
            {
                  dbParam.Direction = System.Data.ParameterDirection.Output;
                  dbParam.DbType = System.Data.DbType.Int16;
                  outputParam = dbParam;
            })

            .ExecuteStoredNonQueryAsync();

      int outputParamValue = (short)outputParam.Value;

```

### Using output parameters without returning a result set but also getting the number of rows affected

Make sure your stored procedure does not contain `SET NOCOUNT ON`.

```csharp
      int numberOfRowsAffected = -1;

      DbParameter outputParam = null;

      var dbContext = GetDbContext();

      numberOfRowsAffected = await dbContext.LoadStoredProc("dbo.SomeSproc")
            .WithSqlParam("InputParam1", 1)
            .WithSqlParam("myOutputParam", (dbParam) =>
            {
                  dbParam.Direction = System.Data.ParameterDirection.Output;
                  dbParam.DbType = System.Data.DbType.Int16;
                  outputParam = dbParam;
            })

            .ExecuteStoredNonQueryAsync();

      int outputParamValue = (short)outputParam.Value;

```

### Changing the execution timeout when waiting for a stored procedure to return

```csharp

      DbParameter outputParam = null;

      var dbContext = GetDbContext();

      // change timeout from 30 seconds to 300 seconds (5 minutes)
      await dbContext.LoadStoredProc("dbo.SomeSproc", commandTimeout:300)
            .WithSqlParam("InputParam1", 1)
            .WithSqlParam("myOutputParam", (dbParam) =>
            {
                  dbParam.Direction = System.Data.ParameterDirection.Output;
                  dbParam.DbType = System.Data.DbType.Int16;
                  outputParam = dbParam;
            })

            .ExecuteStoredNonQueryAsync();

      int outputParamValue = (short)outputParam.Value;

```

### Features

*   Fluent API for stored procedure parameters.
*   Map results to `List<T>`.
*   Map results to `ValueTuple` (e.g., `(int, string)`, `ValueTuple<T1, T2, ...>`).
*   Map results to `DataTable`.
*   Async support.
*   Supports Input and Output parameters.
*   Compatible with Entity Framework Core 8.0 and 9.0.
*   AOT (Ahead-of-Time) compatible.

## Installation

## AOT Compatibility and Source Generation

This library is designed to be fully AOT (Ahead-of-Time) compatible by eliminating runtime reflection for result mapping. It achieves this through a C# Source Generator (`Snickler.EFCore.SourceGenerators`).

**How it works:**

1.  When you compile your project, the source generator analyzes your code for calls to `ReadToList<T>()` and `ReadToValueTupleList<TValueTuple>()`.
2.  For each unique POCO type `T` and `ValueTuple` structure used, it automatically generates dedicated, reflection-free mapping methods.
3.  The original `ReadToList<T>` and `ReadToValueTupleList<TValueTuple>` methods then dispatch to these highly optimized, generated mappers at runtime.

**What this means for you:**

*   **No Runtime Reflection for Mapping:** This ensures better performance and reliability in AOT-compiled applications.
*   **Automatic:** You don't need to write any special code to enable this; just use the library methods as usual.
*   **Build-Time Generation:** All mapping code is generated at build time.
*   **Requirement:** For the source generator to work correctly and discover all types you use for mapping, ensure that the `Snickler.EFCore.SourceGenerators` package (or project reference if building from source) is correctly referenced by any project that calls `ReadToList<T>` or `ReadToValueTupleList<TValueTuple>`, or ensure the types being mapped are visible to the generator during the compilation of the `Snickler.EFCore` library.

The `[DynamicallyAccessedMembers]` attributes previously on `ReadToList<T>` and `ReadToValueTupleList<TValueTuple>` are no longer the primary mechanism for AOT safety for these methods, as the source generator now provides concrete implementations. They may be kept for documentation or as a fallback for scenarios the generator might not cover (though the aim is full coverage).

## Contributing




