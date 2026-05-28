-- Omnijoy database initialization
-- Additional schema migrations are managed via EF Core (see OMNIJOY-3)

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;

-- Ensure the database uses utf8mb4 for emoji support
ALTER DATABASE omnijoy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
