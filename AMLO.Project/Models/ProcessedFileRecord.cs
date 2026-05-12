namespace AMLO.Project.Models
{
    /// <summary>
    /// บันทึกประวัติไฟล์ที่ประมวลผลแล้ว
    /// เพื่อป้องกันการประมวลผลไฟล์เดิมซ้ำ
    /// </summary>
    public class ProcessedFileRecord
    {
        /// <summary>
        /// ชื่อไฟล์ที่ประมวลผล (เช่น Test_Output_Single.csv)
        /// </summary>
        public required string FileName { get; set; }

        /// <summary>
        /// Hash ของไฟล์ เพื่อตรวจจับการเปลี่ยนแปลง
        /// </summary>
        public string FileHash { get; set; }

        /// <summary>
        /// เวลาที่ประมวลผลเสร็จสำเร็จ
        /// </summary>
        public DateTime ProcessedDateTime { get; set; }

        /// <summary>
        /// จำนวนรายการที่ประมวลผลได้
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// สถานะการประมวลผล (Success, Failed, Skipped)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// ความเห็น (เช่น "Duplicate file - skipped" หรือ "Successfully processed")
        /// </summary>
        public string Notes { get; set; }
    }
}
