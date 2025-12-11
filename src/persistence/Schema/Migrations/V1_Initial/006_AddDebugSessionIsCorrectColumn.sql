-- V1_Initial/006_AddDebugSessionIsCorrectColumn.sql
-- Add IsCorrect boolean column to DebugSession table
-- Logic: Mark a debug session as "incorrect" if the detected opponent battleTag 
-- matches the user's own battleTag from settings (indicating a bad detection)

ALTER TABLE DebugSession 
ADD COLUMN IsCorrect BOOLEAN DEFAULT 1;

-- Create index for quick filtering of incorrect detections
CREATE INDEX IF NOT EXISTS IDX_DebugSession_IsCorrect ON DebugSession(IsCorrect DESC);
