using AMLO.Project.Services;
using Microsoft.AspNetCore.Mvc;
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

        // รับ DataProcessingService ผ่าน Constructor (Dependency Injection)
        public AmloProcessDataController(DataProcessingService processService)
        {
            _processService = processService;
        }

        /// <summary>
        /// API สำหรับสั่งรันการนำเข้าไฟล์ CSV
        /// HTTP POST: /api/sync/import-csv
        /// </summary>
        [HttpPost("import-csv")]
        public async Task<IActionResult> ImportCsvAsync()
        {
            try
            {
                // สามารถใช้ Log แทน Console.WriteLine ได้ในอนาคต
                Console.WriteLine($"[API] Starting CSV import ...");

                // เรียกใช้งาน Service ที่เราย้ายมาจาก Program.cs
                await _processService.ImportCsvAsync("*.csv");

                return Ok(new
                {
                    Status = "Success",
                    Message = "CSV data has been successfully imported to SurrealDB."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API ERROR] {ex.Message}");
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
