using MySql.Data.MySqlClient;

namespace CloudSql
{
    public class MySqlUnix
    {
        public static MySqlConnectionStringBuilder NewMysqlUnixSocketConnectionString()
        {
            // Equivalent connection string:
            // "Server=<INSTANCE_UNIX_SOCKET>;Uid=<DB_USER>;Pwd=<DB_PASS>;Database=<DB_NAME>;Protocol=unix"

            var connectionString = new MySqlConnectionStringBuilder()
            {
                // The Cloud SQL proxy provides encryption between the proxy and instance.
                SslMode = MySqlSslMode.Disabled,

                // Note: Saving credentials in environment variables is convenient, but not
                // secure - consider a more secure solution such as
                // Cloud Secret Manager (https://cloud.google.com/secret-manager) to help
                // keep secrets safe.
                Port = UInt32.Parse(Environment.GetEnvironmentVariable("DB_PORT")), // e.g. 3306
                Server = Environment.GetEnvironmentVariable("INSTANCE_UNIX_SOCKET"), // e.g. '/cloudsql/project:region:instance'
                UserID = Environment.GetEnvironmentVariable("DB_USER"),   // e.g. 'my-db-user
                Password = Environment.GetEnvironmentVariable("DB_PASS"), // e.g. 'my-db-password'
                Database = Environment.GetEnvironmentVariable("DB_NAME"), // e.g. 'my-database'
                ConnectionProtocol = MySqlConnectionProtocol.UnixSocket,
            };
            connectionString.Pooling = true;
            // connectionString = $"Server={INSTANCE_UNIX_SOCKET};Uid={DB_USER};Pwd={DB_PASS};Database={DB_NAME};Protocol=unix";

            // Specify additional properties here.
            return connectionString;
        }
        
        public static string GetConnectionString()
        {
            var DB_IP = Environment.GetEnvironmentVariable("DB_IP");
            var DB_USER = Environment.GetEnvironmentVariable("DB_USER");
            var DB_PASS = Environment.GetEnvironmentVariable("DB_PASS");
            var DB_NAME = Environment.GetEnvironmentVariable("DB_NAME");
            var DB_PORT = UInt32.Parse(Environment.GetEnvironmentVariable("DB_PORT"));
            var connectionString = $"Server={DB_IP};Uid={DB_USER};Pwd={DB_PASS};Database={DB_NAME};Port={DB_PORT};Protocol=unix";
            Console.WriteLine(connectionString);
            return connectionString;
        }
    }
}