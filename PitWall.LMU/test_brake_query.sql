-- Test 1: Check what tables exist
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'main' 
ORDER BY table_name;

-- Test 2: Check row counts for session 276
SELECT 'GPS Speed' as table_name, COUNT(*) as row_count FROM "GPS Speed" WHERE session_id = 276
UNION ALL
SELECT 'Brake Pos', COUNT(*) FROM "Brake Pos" WHERE session_id = 276
UNION ALL
SELECT 'Throttle Pos', COUNT(*) FROM "Throttle Pos" WHERE session_id = 276
UNION ALL
SELECT 'Steering Pos', COUNT(*) FROM "Steering Pos" WHERE session_id = 276
UNION ALL
SELECT 'Lap', COUNT(*) FROM "Lap" WHERE session_id = 276;

-- Test 3: Sample brake values
SELECT 'Brake values:' as info;
SELECT value FROM "Brake Pos" WHERE session_id = 276 LIMIT 20;

-- Test 4: Check if there's any other session data
SELECT DISTINCT session_id FROM "Brake Pos" LIMIT 5;
