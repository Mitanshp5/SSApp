using Microsoft.Data.Sqlite;
using SSApp.Data.Models;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SSApp.Data
{
    public static class Database
    {
        private static readonly string _connectionString = "Data Source=ssapp.db";

        // ---------- INIT ----------

        public static void Initialize()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username     TEXT    NOT NULL UNIQUE,
                    PasswordHash TEXT    NOT NULL,
                    Salt         TEXT    NOT NULL,
                    Role         INTEGER NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Ensure at least one admin exists
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = $admin;";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$admin", (int)UserRole.Admin);
            var count = Convert.ToInt64(cmd.ExecuteScalar());

            if (count == 0)
            {
                CreatePasswordHash("admin123", out string hash, out string salt);

                cmd.CommandText = @"
                    INSERT INTO Users (Username, PasswordHash, Salt, Role)
                    VALUES ('admin', $h, $s, $r);";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$h", hash);
                cmd.Parameters.AddWithValue("$s", salt);
                cmd.Parameters.AddWithValue("$r", (int)UserRole.Admin);
                cmd.ExecuteNonQuery();
            }

            // Create PlcConfig table
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PlcConfig (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    IpAddress    TEXT    NOT NULL,
                    Port         INTEGER NOT NULL,
                    LastModified TEXT    NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Create ScanResults table
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ScanResults (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp    TEXT    NOT NULL,
                    InitiatedBy  TEXT    NOT NULL,
                    Status       TEXT    NOT NULL,
                    ResultCode   TEXT    NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        // ---------- PASSWORD HASHING ----------

        private static void CreatePasswordHash(string password, out string hash, out string salt)
        {
            // random 16-byte salt
            using var rng = RandomNumberGenerator.Create();
            byte[] saltBytes = new byte[16];
            rng.GetBytes(saltBytes);

            salt = Convert.ToBase64String(saltBytes);
            hash = ComputeHash(password, saltBytes);
        }

        private static string ComputeHash(string password, byte[] saltBytes)
        {
            byte[] passBytes = Encoding.UTF8.GetBytes(password);
            byte[] combined = new byte[saltBytes.Length + passBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
            Buffer.BlockCopy(passBytes, 0, combined, saltBytes.Length, passBytes.Length);

            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(combined));
        }

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            string computed = ComputeHash(password, saltBytes);
            return hash == computed;
        }

        // ---------- BASIC USER RECORD ----------

        public class UserRecord
        {
            public int Id { get; set; }
            public string Username { get; set; } = "";
            public string PasswordHash { get; set; } = "";
            public string Salt { get; set; } = "";
            public UserRole Role { get; set; }
        }

        // ---------- AUTH / ADMIN COUNT ----------

        public static (bool ok, UserRole role) ValidateUser(string username, string password)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT PasswordHash, Salt, Role
                FROM Users
                WHERE Username = $u;";
            cmd.Parameters.AddWithValue("$u", username);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return (false, UserRole.Viewer);

            string hash = reader.GetString(0);
            string salt = reader.GetString(1);
            var role = (UserRole)reader.GetInt32(2);

            bool ok = VerifyPassword(password, hash, salt);
            return (ok, ok ? role : UserRole.Viewer);
        }

        public static int CountAdmins()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = $r;";
            cmd.Parameters.AddWithValue("$r", (int)UserRole.Admin);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static UserRecord? GetUserById(int id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Username, PasswordHash, Salt, Role
                FROM Users
                WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new UserRecord
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Salt = reader.GetString(3),
                Role = (UserRole)reader.GetInt32(4)
            };
        }

        // ---------- PLC CONFIG ----------

        public static PlcConfig GetPlcConfig()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, IpAddress, Port, LastModified FROM PlcConfig ORDER BY Id DESC LIMIT 1;";
            
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PlcConfig
                {
                    Id = reader.GetInt32(0),
                    IpAddress = reader.GetString(1),
                    Port = reader.GetInt32(2),
                    LastModified = reader.GetString(3)
                };
            }

            // Default values
            return new PlcConfig
            {
                Id = 0,
                IpAddress = "169.254.180.21",
                Port = 5000,
                LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public static void SavePlcConfig(string ip, int port)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            // We'll just insert a new record for history or update the single one. 
            // For simplicity based on previous service code, I'll update if exists or insert.
            // Actually, let's just insert to keep history or update the latest.
            // Given the requirement "store ip and port in database", a single current config is usually enough.
            // But let's check if one exists.
            
            cmd.CommandText = "SELECT COUNT(*) FROM PlcConfig;";
            long count = (long)cmd.ExecuteScalar();

            if (count > 0)
            {
                 cmd.CommandText = @"
                    UPDATE PlcConfig
                    SET IpAddress = $ip, Port = $port, LastModified = $lm
                    WHERE Id = (SELECT MAX(Id) FROM PlcConfig);";
            }
            else
            {
                cmd.CommandText = @"
                    INSERT INTO PlcConfig (IpAddress, Port, LastModified)
                    VALUES ($ip, $port, $lm);";
            }

            cmd.Parameters.AddWithValue("$ip", ip);
            cmd.Parameters.AddWithValue("$port", port);
            cmd.Parameters.AddWithValue("$lm", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            cmd.ExecuteNonQuery();
        }

        // ---------- CRUD ----------

        public static bool AddUser(string username, string password, UserRole role)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            CreatePasswordHash(password, out string hash, out string salt);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, Salt, Role)
                VALUES ($u, $h, $s, $r);";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$s", salt);
            cmd.Parameters.AddWithValue("$r", (int)role);

            try
            {
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE
            {
                return false;
            }
        }

        public static List<UserRecord> GetAllUsers()
        {
            var users = new List<UserRecord>();

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Username, PasswordHash, Salt, Role
                FROM Users
                ORDER BY Role DESC, Username ASC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new UserRecord
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Salt = reader.GetString(3),
                    Role = (UserRole)reader.GetInt32(4)
                });
            }

            return users;
        }

        public static bool UpdateUser(int id, string username, string password, UserRole role)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // if password is empty, don't change hash/salt (optional behaviour)
            string setPasswordPart;
            string hash = "";
            string salt = "";

            if (string.IsNullOrEmpty(password))
            {
                setPasswordPart = "";
            }
            else
            {
                setPasswordPart = ", PasswordHash = $h, Salt = $s";
                CreatePasswordHash(password, out hash, out salt);
            }

            var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                UPDATE Users
                SET Username = $u,
                    Role     = $r
                    {setPasswordPart}
                WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$r", (int)role);
            cmd.Parameters.AddWithValue("$id", id);
            if (!string.IsNullOrEmpty(password))
            {
                cmd.Parameters.AddWithValue("$h", hash);
                cmd.Parameters.AddWithValue("$s", salt);
            }

            try
            {
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return false;
            }
        }

        public static void DeleteUser(int id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Users WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------- SCAN RESULTS ----------

        public static void AddScanResult(ScanRecord record)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ScanResults (Timestamp, InitiatedBy, Status, ResultCode)
                VALUES ($ts, $user, $status, $res);";
            
            cmd.Parameters.AddWithValue("$ts", record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$user", record.InitiatedBy);
            cmd.Parameters.AddWithValue("$status", record.Status);
            cmd.Parameters.AddWithValue("$res", record.ResultCode);

            cmd.ExecuteNonQuery();
        }

        public static List<ScanRecord> GetScanResults()
        {
            var list = new List<ScanRecord>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Timestamp, InitiatedBy, Status, ResultCode FROM ScanResults ORDER BY Id DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ScanRecord
                {
                    Id = reader.GetInt32(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    InitiatedBy = reader.GetString(2),
                    Status = reader.GetString(3),
                    ResultCode = reader.GetString(4)
                });
            }
            return list;
        }
    }
}
