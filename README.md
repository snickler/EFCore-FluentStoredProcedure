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




