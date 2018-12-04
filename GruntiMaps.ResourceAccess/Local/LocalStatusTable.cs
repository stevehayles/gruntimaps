﻿/*

Copyright 2016, 2017, 2018 GIS People Pty Ltd

This file is part of GruntiMaps.

GruntiMaps is free software: you can redistribute it and/or modify it under 
the terms of the GNU Affero General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option) any
later version.

GruntiMaps is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR
A PARTICULAR PURPOSE. See the GNU Affero General Public License for more 
details.

You should have received a copy of the GNU Affero General Public License along
with GruntiMaps.  If not, see <https://www.gnu.org/licenses/>.

*/
using System;
using System.Threading.Tasks;
using GruntiMaps.Common.Enums;
using GruntiMaps.ResourceAccess.Table;
using Microsoft.Data.Sqlite;

namespace GruntiMaps.ResourceAccess.Local
{
    public class LocalStatusTable : IStatusTable
    {
        private readonly SqliteConnection _queueDatabase;

        public LocalStatusTable(string storagePath, string tableName)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DataSource = System.IO.Path.Combine(storagePath, $"{tableName}.table")
            };
            var connStr = builder.ConnectionString;
            _queueDatabase = new SqliteConnection(connStr);
            _queueDatabase.Open();
            const string createStatusesTable = "CREATE TABLE IF NOT EXISTS Statuses(Id NVARCHAR(50) PRIMARY KEY, Status NVARCHAR(50) NOT NULL)";
            new SqliteCommand(createStatusesTable, _queueDatabase).ExecuteNonQuery();
        }

        public Task<LayerStatus?> GetStatus(string id)
        {
            const string getRelatedQueueMsg = "SELECT Status FROM Statuses WHERE Id = $Id";
            var getRelatedQueueCmd = new SqliteCommand(getRelatedQueueMsg, _queueDatabase);
            getRelatedQueueCmd.Parameters.AddWithValue("$Id", id);
            var relatedQueueReader = getRelatedQueueCmd.ExecuteReader();
            if (relatedQueueReader.HasRows)
            {
                Enum.TryParse(relatedQueueReader["Status"].ToString(), out LayerStatus status);
                return Task.FromResult<LayerStatus?>(status);
            }

            return Task.FromResult<LayerStatus?>(null);
        }

        public async Task UpdateStatus(string id, LayerStatus status)
        {
            var currentStatus = await GetStatus(id);

            string msg;
            if (!currentStatus.HasValue)
            {
                // create one if status doesn't exist
                msg = "INSERT INTO Statuses (Id, Status) VALUES($Id, $Status)";
            }
            else
            {
                // update it if it exists
                msg = "UPDATE Statuses SET Status = $Status WHERE Id = $Id";
            }
            var cmd = new SqliteCommand(msg, _queueDatabase);
            cmd.Parameters.AddWithValue("$Status", status.ToString());
            cmd.Parameters.AddWithValue("$Id", id);
            cmd.ExecuteScalar();
        }

        public void Clear()
        {
            new SqliteCommand("DELETE FROM Statuses", _queueDatabase).ExecuteNonQuery();
        }
    }
}
