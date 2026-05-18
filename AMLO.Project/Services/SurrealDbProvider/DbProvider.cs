using AMLO.Project.Helpers;
using Mapster;
using SurrealDb.Net;
using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Response;
using System.Linq;

namespace AMLO.Project.Services.SurrealDbProvider;

/// <summary>
/// The database provider interface for SurrealDB operations
/// </summary>
/// <typeparam name="TsurrealModel">SurrealDB model type</typeparam>
/// <typeparam name="TamloModel">Application model type</typeparam>
public interface IDbProvider<TsurrealModel, TamloModel>
    where TsurrealModel : class, IRecord
    where TamloModel : class
{
    /// <summary>
    /// Table name
    /// </summary>
    string Table { get; }

    /// <summary>
    /// Get all records from the table
    /// </summary>
    Task<IEnumerable<TsurrealModel>> List(CancellationToken cancellationToken = default);
    Task<IEnumerable<TamloModel>> ListX(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single record by ID
    /// </summary>
    Task<TsurrealModel?> Get(string id, CancellationToken cancellationToken = default);
    Task<TamloModel?> GetX(string id, CancellationToken cancellationToken = default);
    Task<TsurrealModel?> Get(RecordId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new record
    /// </summary>
    Task<TsurrealModel> Create(TsurrealModel data, CancellationToken cancellationToken = default);
    Task<TamloModel> CreateX(TamloModel data, CancellationToken cancellationToken = default);
    Task<IEnumerable<TamloModel>> CreateManyX(IEnumerable<TamloModel> datas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create or update a record
    /// </summary>
    Task<TsurrealModel> Upsert(TsurrealModel data, CancellationToken cancellationToken = default);
    Task<TamloModel> UpsertX(TamloModel data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a record
    /// </summary>
    Task<TsurrealModel> Update(string id, Dictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task<TamloModel> UpdateX(string id, Dictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task<TsurrealModel> Update(RecordId id, Dictionary<string, object?> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a raw query
    /// </summary>
    Task<SurrealDbResponse> Query(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a query and return results
    /// </summary>
    Task<IEnumerable<TamloModel>> Select(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default);
    Task<IEnumerable<TamloModel>> SelectIndex(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, int qryIndex = 0, CancellationToken cancellationToken = default);
    Task<TamloModel> SelectOne(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default);
    Task<TamloModel> SelectOneIndex(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, int qryIndex = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a generic query
    /// </summary>
    Task<IEnumerable<Ts>?> Query<Ts>(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default);
    Task<Ts?> QueryOne<Ts>(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default) where Ts : class;
}

/// <summary>
/// Implementation of IDbProvider for SurrealDB
/// </summary>
public class DbProvider<TsurrealModel, TamloModel> : IDbProvider<TsurrealModel, TamloModel>
    where TsurrealModel : class, IRecord
    where TamloModel : class
{
    private readonly ISurrealDbClient _surrealDbClient;

    public DbProvider(ISurrealDbClient surrealDbClient)
    {
        _surrealDbClient = surrealDbClient;
    }

    public string Table => typeof(TsurrealModel).Name;

    public async Task<IEnumerable<TsurrealModel>> List(CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Select<TsurrealModel>(Table, cancellationToken);
    }

    public async Task<IEnumerable<TamloModel>> ListX(CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Select<TamloModel>(Table, cancellationToken);
    }

    public async Task<TsurrealModel?> Get(string id, CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Select<TsurrealModel>((Table, id), cancellationToken);
    }

    public async Task<TamloModel?> GetX(string id, CancellationToken cancellationToken)
    {
        var data = await _surrealDbClient.Select<TsurrealModel>((Table, id), cancellationToken);
        return data?.Adapt<TamloModel>();
    }

    public async Task<TsurrealModel?> Get(RecordId id, CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Select<TsurrealModel>(id, cancellationToken);
    }

    public async Task<TsurrealModel> Create(TsurrealModel data, CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Create(Table, data, cancellationToken);
    }

    public async Task<TamloModel> CreateX(TamloModel data, CancellationToken cancellationToken)
    {
        var srData = data.Adapt<TsurrealModel>();
        var createdSrData = await _surrealDbClient.Create(Table, srData, cancellationToken);
        return createdSrData.Adapt<TamloModel>();
    }

    public async Task<IEnumerable<TamloModel>> CreateManyX(IEnumerable<TamloModel> datas, CancellationToken cancellationToken = default)
    {
        var dataList = datas as IList<TamloModel> ?? datas.ToList();
        if (dataList.Count == 0)
        {
            return Enumerable.Empty<TamloModel>();
        }

        var returnData = new List<TamloModel>(dataList.Count);

        foreach (var batch in dataList.Chunk(BatchSizeConstants.SurrealSafeBatchSize))
        {
            var surrealBatch = batch.Adapt<List<TsurrealModel>>();
            var insertedBatch = await _surrealDbClient.Insert(Table, surrealBatch, cancellationToken);
            returnData.AddRange(insertedBatch.Select(s => s.Adapt<TamloModel>()));
        }

        return returnData;
    }

    public async Task<TsurrealModel> Upsert(TsurrealModel data, CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Upsert(data, cancellationToken);
    }

    public async Task<TamloModel> UpsertX(TamloModel data, CancellationToken cancellationToken)
    {
        var x = data.Adapt<TsurrealModel>();
        var y = await _surrealDbClient.Upsert(x, cancellationToken);
        return y.Adapt<TamloModel>();
    }

    public async Task<TsurrealModel> Update(string id, Dictionary<string, object?> data, CancellationToken cancellationToken)
    {
        var thing = RecordId.From(Table, id);
        return await _surrealDbClient.Merge<TsurrealModel>(thing, data, cancellationToken);
    }

    public async Task<TamloModel> UpdateX(string id, Dictionary<string, object?> data, CancellationToken cancellationToken)
    {
        var thing = RecordId.From(Table, id);
        var x = await _surrealDbClient.Merge<TsurrealModel>(thing, data, cancellationToken);
        return x.Adapt<TamloModel>();
    }

    public async Task<TsurrealModel> Update(RecordId id, Dictionary<string, object?> data, CancellationToken cancellationToken)
    {
        return await _surrealDbClient.Merge<TsurrealModel>(id, data, cancellationToken);
    }

    public async Task<SurrealDbResponse> Query(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default)
    {
        return await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken);
    }

    public async Task<IEnumerable<Ts>?> Query<Ts>(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default)
    {
        var documents = await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken);
        return documents.GetValue<IEnumerable<Ts>?>(0);
    }

    public async Task<Ts?> QueryOne<Ts>(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default) where Ts : class
    {
        var documents = await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken);
        var list = documents.GetValue<IEnumerable<Ts>?>(0);
        return list?.FirstOrDefault();
    }

    public async Task<IEnumerable<TamloModel>> Select(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default)
    {
        var data = (await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken)).GetValue<IEnumerable<TsurrealModel>>(0);
        return data.Adapt<IEnumerable<TamloModel>>();
    }

    public async Task<IEnumerable<TamloModel>> SelectIndex(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, int qryIndex = 0, CancellationToken cancellationToken = default)
    {
        var data = (await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken)).GetValue<IEnumerable<TsurrealModel>>(qryIndex);
        return data.Adapt<IEnumerable<TamloModel>>();
    }

    public async Task<TamloModel> SelectOne(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, CancellationToken cancellationToken = default)
    {
        var data = (await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken)).GetValue<IEnumerable<TsurrealModel>>(0).FirstOrDefault();
        return data?.Adapt<TamloModel>()!;
    }

    public async Task<TamloModel> SelectOneIndex(FormattableString qry, IReadOnlyDictionary<string, object?>? parameters = default, int qryIndex = 0, CancellationToken cancellationToken = default)
    {
        var data = (await _surrealDbClient.RawQuery(qry.ToString(), parameters, cancellationToken)).GetValue<IEnumerable<TsurrealModel>>(qryIndex).FirstOrDefault();
        return data?.Adapt<TamloModel>()!;
    }
}
