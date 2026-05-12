-- SurrealDB Schema for Processed Files Tracking
-- Execute this script to create the processed_files table

-- Create table for tracking processed CSV files
DEFINE TABLE processed_files SCHEMAFULL 
    PERMISSIONS 
        FOR select FULL,
        FOR create FULL,
        FOR update FULL,
        FOR delete FULL;

-- Field: fileName - ชื่อไฟล์ที่ประมวลผล
DEFINE FIELD fileName ON processed_files 
    TYPE string
    ASSERT $value != null AND $value != ""
    COMMENT "ชื่อไฟล์ที่ประมวลผล เช่น Test_Output_Single.csv";

-- Field: fileHash - Hash ของไฟล์ (สำหรับตรวจจับการเปลี่ยนแปลง)
DEFINE FIELD fileHash ON processed_files 
    TYPE string
    VALUE $null
    COMMENT "Hash ของไฟล์ (MD5 หรือ SHA256)";

-- Field: processedDateTime - เวลาที่ประมวลผลไฟล์
DEFINE FIELD processedDateTime ON processed_files 
    TYPE datetime
    VALUE time::now()
    COMMENT "เวลาที่ประมวลผลไฟล์ (UTC)";

-- Field: recordCount - จำนวนรายการที่ประมวลผลได้
DEFINE FIELD recordCount ON processed_files 
    TYPE number
    VALUE 0
    COMMENT "จำนวนรายการที่ประมวลผลได้";

-- Field: status - สถานะการประมวลผล
DEFINE FIELD status ON processed_files 
    TYPE string
    ENUM 'Success', 'Failed', 'Skipped'
    VALUE 'Success'
    COMMENT "สถานะการประมวลผล: Success (สำเร็จ), Failed (ล้มเหลว), Skipped (ข้าม)";

-- Field: notes - ความเห็นเพิ่มเติม
DEFINE FIELD notes ON processed_files 
    TYPE string
    VALUE null
    COMMENT "ความเห็นเพิ่มเติมเช่น 'Duplicate file - already processed' หรือ 'Error message'";

-- Create unique index for faster lookup
DEFINE INDEX idx_processed_files_fileName_status 
    ON processed_files 
    COLUMNS fileName, status 
    UNIQUE;

-- Create index for date range queries
DEFINE INDEX idx_processed_files_datetime 
    ON processed_files 
    COLUMNS processedDateTime;

-- Create index for status queries
DEFINE INDEX idx_processed_files_status 
    ON processed_files 
    COLUMNS status;

-- ======================================================================
-- Example Queries to Test
-- ======================================================================

-- 1. ค้นหาไฟล์ที่เคยประมวลผลสำเร็จ
-- SELECT * FROM processed_files WHERE fileName = 'Test_Output_Single.csv' AND status = 'Success';

-- 2. ดูประวัติการประมวลผลทั้งหมด
-- SELECT fileName, status, processedDateTime, recordCount FROM processed_files ORDER BY processedDateTime DESC;

-- 3. ดูไฟล์ที่ข้ามไป (Skipped)
-- SELECT * FROM processed_files WHERE status = 'Skipped' ORDER BY processedDateTime DESC;

-- 4. ดูไฟล์ที่ประมวลผลล้มเหลว
-- SELECT * FROM processed_files WHERE status = 'Failed' ORDER BY processedDateTime DESC;

-- 5. ดูจำนวนไฟล์ที่ประมวลผลแต่ละสถานะ
-- SELECT status, count(*) as count FROM processed_files GROUP BY status;

-- 6. ดูไฟล์ที่ประมวลผลในช่วง 24 ชั่วโมงที่ผ่านมา
-- SELECT * FROM processed_files 
-- WHERE processedDateTime > time::now() - 1d 
-- ORDER BY processedDateTime DESC;

-- 7. ค้นหาไฟล์ตามชื่อ (pattern matching)
-- SELECT * FROM processed_files WHERE fileName CONTAINS 'Test_' ORDER BY processedDateTime DESC;
