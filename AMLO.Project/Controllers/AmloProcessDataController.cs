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
        public async Task<IActionResult> ImportCsvAsync([FromBody] ImportCsvRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "ไม่สามารถดำเนินการได้: เนื่องจากข้อมูล 'fileName' ใน Request Body เป็นค่าว่างหรือรูปแบบไม่ถูกต้อง"
                });
            }

            try
            {
                _logger.LogInformation("[API] Starting CSV import for explicit file: {FileName}", request.FileName);

                // เรียกใช้งาน Service
                await _processService.ImportCsvAsync(request.FileName);

                return Ok(new
                {
                    Status = "Success",
                    Message = $"ไฟล์ '{request.FileName}' ได้รับการประมวลผลและบันทึกลง SurrealDB เรียบร้อยแล้ว"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API ERROR] การประมวลผลไฟล์ {FileName} ล้มเหลว: {Message}", request.FileName, ex.Message);
                // หากเกิด Error จะส่ง HTTP Status 500 กลับไปให้ฝั่งที่เรียก API
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = ex.Message,
                    InnerError = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Data Transfer Object สำหรับรับข้อมูลจากฝั่งขอยิง API
        /// </summary>
        public class ImportCsvRequest
        {
            public string FileName { get; set; } = string.Empty;
        }
    }
}
