using AMLO.Project.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AMLO.Project.Controllers
{
    [ApiController]
    [Route("api/processData")]
    public class AmloProcessDataController : ControllerBase
    {
        private readonly DataProcessingService _processService;
        private readonly ILogger<AmloProcessDataController> _logger;

        // รับ DataProcessingService ผ่าน Constructor (Dependency Injection)
        public AmloProcessDataController(
            DataProcessingService processService,
            ILogger<AmloProcessDataController> logger)
        {
            _processService = processService;
            _logger = logger;
        }

        /// <summary>
        /// API สำหรับสั่งรันการนำเข้าไฟล์ CSV
        /// HTTP POST: /api/processData/import-csv
        /// </summary>
        [HttpPost("import-csv")]
        public async Task<IActionResult> ImportCsvAsync()
        {
            try
            {
                _logger.LogInformation($"[API] Starting CSV import ...");

                // เรียกใช้งาน Service
                await _processService.ImportCsvAsync("*.csv");

                return Ok(new
                {
                    Status = "Success",
                    Message = "CSV data has been successfully imported to SurrealDB."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[API ERROR] {ex.Message}");
                // หากเกิด Error จะส่ง HTTP Status 500 กลับไปให้ฝั่งที่เรียก API
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = ex.Message,
                    InnerError = ex.InnerException?.Message
                });
            }
        }
    }
}
