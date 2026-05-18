using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net.Models;
namespace AMLO.Project.Services.SurrealDbProvider;

/// <summary>
/// SurrealDb Provider Factory Base
/// </summary>
public abstract class SurrealDbProviderFactoryBase
{
    /// <summary>
    /// Create database provider for a table
    /// </summary>
    public abstract IDbProvider<TSurrealModel, TamloModel> Create<TSurrealModel, TamloModel>() where TSurrealModel : class, IRecord where TamloModel : class;
}

/// <summary>
/// SurrealDb Provider Factory implementation
/// </summary>
public sealed class SurrealDbProviderFactory : SurrealDbProviderFactoryBase
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// SurrealDb Provider Factory constructor
    /// </summary>
    public SurrealDbProviderFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Create a database provider instance
    /// </summary>
    public sealed override IDbProvider<TSurrealModel, TamloModel> Create<TSurrealModel, TamloModel>()
    {
        var dbProvider = _provider.GetService<IDbProvider<TSurrealModel, TamloModel>>();
        if (dbProvider == null)
            throw new ArgumentNullException(nameof(dbProvider), "IDbProvider is not registered in the service container.");
        return dbProvider;
    }
}
