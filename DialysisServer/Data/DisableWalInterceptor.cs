using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace DialysisServer.Data
{
    public sealed class DisableWalInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (connection is SqliteConnection sqlite)
            {
                using var command = sqlite.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=DELETE;";
                command.ExecuteNonQuery();
            }

            base.ConnectionOpened(connection, eventData);
        }
    }
}
