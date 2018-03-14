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

