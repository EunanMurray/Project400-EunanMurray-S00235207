var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var sqlDatabase = sqlServer.AddDatabase("project400db");

var keyVault = builder.AddConnectionString("keyvault");

var api = builder.AddProject<Projects.Project400API>("project400api")
    .WithReference(sqlDatabase)
    .WithReference(keyVault);

builder.AddProject<Projects.Project400Web>("project400web")
    .WithReference(api);

builder.Build().Run();
