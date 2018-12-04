﻿using System;
using System.Threading.Tasks;
using GruntiMaps.Common.Enums;
using GruntiMaps.ResourceAccess.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;

namespace GruntiMaps.ResourceAccess.Azure
{
    public class AzureStatusTable : IStatusTable
    {
        public const string Workspace = "Workspace"; // this is to be replaced when adding workspace function

        private readonly CloudTable _table;

        public AzureStatusTable(string storageAccount, string storageKey, string tableName)
        {
            var account = new CloudStorageAccount(new StorageCredentials(storageAccount, storageKey), true);
            var client = account.CreateCloudTableClient();

            _table = client.GetTableReference(tableName);
            _table.CreateIfNotExistsAsync();
        }

        public async Task<LayerStatus?> GetStatus(string id)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<StatusEntity>(Workspace, id);

            TableResult retrievedResult = await _table.ExecuteAsync(retrieveOperation);

            if (retrievedResult.Result == null)
            {
                return null;
            }

            Enum.TryParse(((StatusEntity)retrievedResult.Result).Status, out LayerStatus status);
            return status;
        }

        public async Task UpdateStatus(string id, LayerStatus status)
        {

            TableOperation retrieveOperation = TableOperation.Retrieve<StatusEntity>(Workspace, id);

            TableResult retrievedResult = await _table.ExecuteAsync(retrieveOperation);

            if (retrievedResult.Result != null)
            {
                var queue = (StatusEntity)retrievedResult.Result;
                queue.Status = status.ToString();
                await _table.ExecuteAsync(TableOperation.Replace(queue));
            }
            else
            {
                await _table.ExecuteAsync(TableOperation.Insert(new StatusEntity(id)));
            }
        }
    }

    public class StatusEntity : TableEntity
    {
        public StatusEntity(string id)
        {
            PartitionKey = AzureStatusTable.Workspace;
            RowKey = id;
            Id = id;
            Status = LayerStatus.Processing.ToString();
        }

        public StatusEntity() { }

        public string Id { get; set; }

        public string Status { get; set; }
    }
}