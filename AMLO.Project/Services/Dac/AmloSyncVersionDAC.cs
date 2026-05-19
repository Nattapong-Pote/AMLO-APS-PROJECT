using System;
using System.Collections.Generic;
using System.Text;
using AMLO.Project.Models;
using AMLO.Project.Services.SurrealDbProvider;

namespace AMLO.Project.Services.Dac
{
    public interface IAmloSyncVersionDAC
    {
        Task<List<AmloVersionRecordDto>> GetAllAsync();
        Task<AmloVersionRecordDto?> GetByNameAsync(string name);
        Task<AmloVersionRecordDto?> UpdateAmloVersionRecordAsync(string Id, Dictionary<string, object?> dictionary);
        Task<AmloVersionRecordDto?> CreateAmloVersionRecordAsync(AmloVersionRecordDto amloVersionRecordDto);
    }
    public class AmloSyncVersionDAC : IAmloSyncVersionDAC
    {
        private readonly IDbProvider<AmloVersionRecord, AmloVersionRecordDto> _dbProvider;

        public AmloSyncVersionDAC(SurrealDbProviderFactoryBase surrealDbProviderFactory)
        {
            _dbProvider = surrealDbProviderFactory.Create<AmloVersionRecord, AmloVersionRecordDto>();
        }
        public async Task<List<AmloVersionRecordDto>> GetAllAsync()
        {
            var obj = await _dbProvider.ListAsAmloModelAsync();
            return obj.ToList();
        }
        public async Task<AmloVersionRecordDto?> GetByNameAsync(string name)
        {
            var allRecords = await _dbProvider.ListAsAmloModelAsync();
            return allRecords.FirstOrDefault(x => x.Name == name);
        }
        public async Task<AmloVersionRecordDto?> UpdateAmloVersionRecordAsync(string Id, Dictionary<string, object?> dictionary)
        {
            var updatedRecord = await _dbProvider.UpdateAsAmloModelAsync(Id, dictionary, CancellationToken.None);
            return updatedRecord;
        }
        public async Task<AmloVersionRecordDto?> CreateAmloVersionRecordAsync(AmloVersionRecordDto amloVersionRecordDto)
        {
            var createdRecord = await _dbProvider.CreateAsAmloModelAsync(amloVersionRecordDto);
            return createdRecord;
        }
    }
}
