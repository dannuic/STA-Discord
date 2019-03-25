using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AirtableApiClient;
using Serilog;

namespace STA_Discord
{
    public class Airtable
    {
        private AirtableBase _base;

        public Config Config { get; set; }

        public Airtable(Config config)
        {
            Config = config;
            _base = new AirtableBase(Config.AirtableToken, Config.AirtableBaseId);
        }

        public async Task<Record> Get(string table, string id)
        {
            AirtableRetrieveRecordResponse response = await _base.RetrieveRecord(table, id);

            if (!response.Success)
            {
                if (response.AirtableApiError is AirtableApiException)
                {
                    Log.Error("Failed to retrieve record {table}.{id} with error {message}", table, id, response.AirtableApiError.ErrorMessage);
                }
                else
                {
                    Log.Error("Failed to retrieve record {table}.{id} with unknown error", table, id);
                }

                return null;
            }
            else
            {
                return new Record(response.Record);
            }
        }

        public async Task<List<Record>> Get(string table, List<string> fields = null)
        {
            string offset = null;
            AirtableListRecordsResponse response = await _base.ListRecords(table, offset, fields);

            if (!response.Success)
            {
                if (response.AirtableApiError is AirtableApiException)
                {
                    Log.Error("Failed to retrieve records in table {table} with error {message}", table, response.AirtableApiError.ErrorMessage);
                }
                else
                {
                    Log.Error("Failed to retrieve records in table {table} with unknown error", table);
                }

                return new List<Record>();
            }
            else
            {
                return response.Records.Select(ar => new Record(ar)).ToList();
            }
        }

        public async Task<Record> Update(string table, string id, List<(string field, string content)> entries)
        {
            Fields fields = new Fields();
            foreach (var entry in entries) {
                fields.AddField(entry.field, entry.content);
            }

            AirtableCreateUpdateReplaceRecordResponse response = await _base.UpdateRecord(table, fields, id, true);

            if (!response.Success)
            {
                if (response.AirtableApiError is AirtableApiException)
                {
                    Log.Error("Failed to update fields [{fields}] in table {table} with id {id} with error {message}",
                        string.Join(", ", entries.Select(entry => string.Join(" -> ", entry.field, entry.content))),
                        table, id, response.AirtableApiError.ErrorMessage);
                }
                else
                {
                    Log.Error("Failed to update fields [{fields}] in table {table} with id {id} with unknown error",
                        string.Join(", ", entries.Select(entry => string.Join(" -> ", entry.field, entry.content))),
                        table, id);
                }

                return null;
            }
            else
            {
                return new Record(response.Record);
            }
        }

        public class Record
        {
            private AirtableRecord _record;

            public Record(AirtableRecord record)
            {
                _record = record;
            }

            public T Get<T>(string key, T def)
            {
                var result = _record.GetField(key);
                return result is null ? def : (T)result;
            }

            public object Get(string key)
            {
                return _record.GetField(key);
            }

            public string Id()
            {
                return _record.Id;
            }
        }
    }
}
