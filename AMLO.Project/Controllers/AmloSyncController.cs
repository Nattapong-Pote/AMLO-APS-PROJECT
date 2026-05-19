using System;
using System.Collections.Generic;
using System.Text;
using AMLO.Project.Models;
using AMLO.Project.Services;
using Microsoft.AspNetCore.Mvc;

namespace AMLO.Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AmloSyncController : ControllerBase
    {
        public readonly IAmloSyncService amloSyncService ;

        public AmloSyncController(IAmloSyncService amloSyncService)
        {
            this.amloSyncService = amloSyncService;
        }

        [HttpGet("SyncAllParallelAsync")]
        public async Task<string> SyncAllParallelAsync()
        {
            return await this.amloSyncService.SyncAllParallelAsync();
        }
        [HttpGet("GetAllData")]
        public async Task<List<AmloVersionRecordDto>> GetAllData()
        {
            return await this.amloSyncService.GetAllVersionFromDbAsync();
        }

    }
}
