using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedIrbis;
using ManagedIrbis.Batch;
using Nest;
using System.Linq;
using System.Text.RegularExpressions;
using static IrbisOpenSearch.SearchRecord;

using ManagedIrbis.Search;
using System.Text;
using System.IO;
using ManagedIrbis.Fields;
using System.Configuration;
using Elasticsearch.Net;
using System.Security.Cryptography.X509Certificates;

namespace IrbisOpenSearch
{
    internal static class Program
    {
        private const int IRBIS_BATCH_SIZE = 500;
        private const int ElasticSearch_BATCH_SIZE = 1000;
        private const string INDEX_NAME = "irbis_index";

        private static string logFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            string.Format("IrbisElasticSearch_log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss")));

        private static async Task Main(string[] args)
        {
            var irbisConnectionString = "server=127.0.0.1;port=8888;user=СПА;password=4209;arm=R";
            var irbisConnection = new IrbisConnection(irbisConnectionString);

            var elasticClient = GetElasticClient();
            if (!elasticClient.Indices.Exists(INDEX_NAME).Exists)
            {
                var createIndexResponse = elasticClient.Indices.Create(INDEX_NAME, c => c
                     .Map<SearchRecord>(m => m.AutoMap()));
                if (!createIndexResponse.IsValid)
                {
                    string msg = $"Error creating index: {createIndexResponse}";
                    logAndExit(msg);
                }
            }

            var mappingResponse = elasticClient.Indices
                .PutMapping<SearchRecord>(mapping => mapping.Index(INDEX_NAME));
            Console.WriteLine($"ElasticSearch mapping: {mappingResponse}");
            WriteToLogFile($"ElasticSearch mapping: {mappingResponse}");

            SearchInIrbisAndSaveToElasticSearch(elasticClient, irbisConnection);
            irbisConnection.Dispose();
        }

        private static ElasticClient GetElasticClient()
        {
            ElasticClient client;
            try
            {
                string password = ConfigurationManager.AppSettings["Password"];

                var pool = new SingleNodeConnectionPool(new Uri("https://localhost:9200"));
                var connectionSettings = new Nest.ConnectionSettings(pool)
                    .DefaultIndex(INDEX_NAME)
                    .BasicAuthentication("elastic", password) // replace with your username and password
                    .ServerCertificateValidationCallback((o, certificate, chain, errors) =>
                    {
                        // Load the CA root certificate
                        var caRootCertificate = new X509Certificate2("c:\\elasticsearch-8.8.1\\config\\certs\\http_ca.crt");

                        // Validate the server's certificate against the CA's root certificate
                        return chain.ChainElements[chain.ChainElements.Count - 1].Certificate.RawData.SequenceEqual(caRootCertificate.RawData);
                    });

                client = new ElasticClient(connectionSettings);
                var pingResponse = client.Ping();
                if (!pingResponse.IsValid) logAndExit(pingResponse.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("ElasticSearch connection failed: " + ex.Message);
                WriteToLogFile("ElasticSearch connection failed: " + ex.Message);
                client = new ElasticClient();
            }
            return client;
        }

        public static void SearchInIrbisAndSaveToElasticSearch(ElasticClient elasticClient, IrbisConnection connection)
        {
            //TEST LAUNCH
            bool isTestLaunch = false;

            List<string> dbNames = new List<string> { "MPDA", "DST", "COLL", "PR", "KJRNMDA" };

            foreach (var dbName in dbNames)
            {
                Console.WriteLine(DateTime.Now); //20:07
                WriteToLogFile(DateTime.Now.ToString()); //20:07
                connection.Database = dbName;
                List<SearchRecord> searchRecords = new List<SearchRecord>();
                try
                {
                    int maxMfn = connection.GetMaxMfn();
                    Console.WriteLine("Connect to Database {0}, Max mfn = {1}", dbName, maxMfn);
                    WriteToLogFile(string.Format("Connect to Database {0}, Max mfn = {1}", dbName, maxMfn));
                    if (maxMfn != 0)
                    {
                        SearchParameters parameters = new SearchParameters { Database = dbName, SequentialSpecification = "v920<>''" };
                        int[] mfns = connection.SequentialSearch(parameters);
                        if (isTestLaunch)
                        {
                            mfns = Enumerable.Range(1, 10).ToArray();
                        }
                        BatchRecordReader reader = new BatchRecordReader(connection, connection.Database, IRBIS_BATCH_SIZE, mfns);

                        foreach (MarcRecord record in reader)
                        {
                            RecordField field = record.Fields.GetField(900, 0) ?? new RecordField();
                            CodesInfo codes = CodesInfo.Parse(field);
                            var searchRecord = new SearchRecord
                            {
                                Id = connection.Database + record.Mfn,
                                FullDescription = GetFullDescription(connection, record),
                                Authors = GetTextFromInfoFormat(connection, record, "Автор(ы)"),
                                Title = GetTextFromInfoFormat(connection, record, "Заглавие"),
                                Imprints = GetImprints(record),
                                RawRecord = GetRawRecord(connection, record),
                                MFN = record.Mfn,
                                Version = record.Version,
                                InvNums = GetInvNums(record),
                                KeyWords = GetKeyWords(record),
                                CataloguersData = GetCataloguersData(record),
                                Annotation = record.FM(331),
                                Contents = record.FMA(330, 'c'),
                                DocumentCharacters = GetDocChars(codes),
                                DocKind = codes.DocumentKind,
                                DocType = codes.DocumentType
                            };
                            //searchRecords.Add(searchRecord);
                            UpsertRecord(searchRecord, elasticClient);
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.Format("No records found in Database {0}", connection.Database));
                        WriteToLogFile(string.Format("No records found in Database {0}", connection.Database));
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Файл не существует")
                    {
                        continue;
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                        WriteToLogFile(ex.Message + "\n" + ex.StackTrace);
                    }
                    Console.WriteLine($"Error querying database: {ex}");
                    WriteToLogFile($"Error querying database: {ex}");
                }
                //IndexRecordsIntoElasticSearch(elasticClient, searchRecords);
            }
        }

        private static string[] GetDocChars(CodesInfo codes)
        {
            return new[]{codes.DocumentCharacter1,
                         codes.DocumentCharacter2,
                         codes.DocumentCharacter3,
                         codes.DocumentCharacter4,
                         codes.DocumentCharacter5,
                         codes.DocumentCharacter6};
        }

        private static string GetRawRecord(IrbisConnection connection, MarcRecord record)
        {
            string rawRecord = connection.FormatRecord("@all", record).Replace("\\par", "\n");
            rawRecord = Regex.Replace(rawRecord, @"\\tx200|_\\b0|\\b0|\\b|\{|\}", "");
            return rawRecord;
        }

        private static Imprint[] GetImprints(MarcRecord record)
        {
            List<Imprint> cataloguers = new List<Imprint>();
            foreach (var field in record.Fields.GetField(210))
            {
                var cataloguer = new Imprint
                {
                    Location = field.GetFirstSubFieldValue('a'),
                    Publisher = field.GetFirstSubFieldValue('c'),
                    Year = field.GetFirstSubFieldValue('d')
                };
                cataloguers.Add(cataloguer);
            }
            return cataloguers.ToArray();
        }

        private static string[] GetContents(MarcRecord record)
        {
            List<string> contents = new List<string>();
            foreach (var field in record.Fields.GetField(330))
            {
                contents.Add(field.GetFirstSubFieldValue('c'));
            }
            return contents.ToArray();
        }

        private static string GetAnnotaion(MarcRecord record)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var field in record.Fields.GetField(331))
            {
                sb.Append(field.Value);
            }
            return sb.ToString();
        }

        private static Cataloguer[] GetCataloguersData(MarcRecord record)
        {
            List<Cataloguer> cataloguers = new List<Cataloguer>();
            foreach (var field in record.Fields.GetField(907))
            {
                var cataloguer = new Cataloguer
                {
                    Date = field.GetFirstSubFieldValue('a'),
                    Initials = field.GetFirstSubFieldValue('b'),
                    StageOfWork = field.GetFirstSubFieldValue('c')
                };
                cataloguers.Add(cataloguer);
            }
            return cataloguers.ToArray();
        }

        private static string[] GetKeyWords(MarcRecord record)
        {
            return record.FMA(610);
        }

        private static InventoryNumber[] GetInvNums(MarcRecord record)
        {
            List<InventoryNumber> invNums = new List<InventoryNumber>();
            foreach (var field in record.Fields.GetField(910))
            {
                var invNum = new InventoryNumber
                {
                    Date = field.GetFirstSubFieldValue('c'),
                    Number = field.GetFirstSubFieldValue('b'),
                    KSU = field.GetFirstSubFieldValue('u'),
                    Channel = field.GetFirstSubFieldValue('f')
                };
                invNums.Add(invNum);
            }
            return invNums.ToArray();
        }

        private static string GetYear(IrbisConnection connection, MarcRecord record)
        {
            return Regex.Match(GetTextFromInfoFormat(connection, record, "Выходные данные"),
                @",\s*(\d{4})").Groups[1].Value;
        }

        private static string GetTextFromInfoFormat(IrbisConnection connection, MarcRecord record, string fieldName)
        {
            string infoFormat = connection.FormatRecord("@infow_h", record);
            fieldName = fieldName.Replace("(", "\\(").Replace(")", "\\)");
            string pattern = @"<b> " + fieldName + @"<\/b> :(.*?)(?=<br>)";
            string text = Regex.Match(infoFormat, pattern, RegexOptions.Singleline)
                                        .Groups[1].Value.Trim();
            return text;
        }

        public static string GetFullDescription(IrbisConnection connection, MarcRecord record)
        {
            string description = connection.FormatRecord("@", record).Replace("<table width=\"100%\"><tr><td   valign=\"top\">", "");
            description = Regex.Replace(description, @"<br>|<dd>|<td>|\\pard|\\par|\\tab", "\n");
            description = Regex.Replace(description, @"(\r?\s*\n|\n\s*\n|\n){2,}", "\n");
            description = Regex.Replace(description, @"<DIV ALIGN=CENTER>|</>|<DIV ALIGN=LEFT>|\\tx200|</?b>|\\b0|\\b|!Ofinal.pft: FILE NOT FOUND!|
</>|</td>|</tr>|</table>|\{|\}", "");
            description = Regex.Replace(description, "<A\\s+HREF=\\\\?\"IRBIS:3,12,,\\d+,1\\\\?\"><IMG\\s+style=\\\\?\"width:105\\s+px\\\\?\"\\s+SRC=\\\\?\"IRBIS:12,,\\d+,1\\\\?\"><\\/A>", "");
            description = Regex.Replace(description, @"(\s*\n|\n\s*){2,}", "\n");

            return description;
        }

        private static void IndexRecordsIntoElasticSearch(ElasticClient elasticClient, IEnumerable<SearchRecord> searchRecords)
        {
            try
            {
                var mappingResponse = elasticClient.Indices
                    .PutMapping<SearchRecord>(mapping => mapping.Index(INDEX_NAME));
                Console.WriteLine(mappingResponse);
                WriteToLogFile(mappingResponse.ToString());
                Console.WriteLine("Saving {0} search records to index {1}", searchRecords.Count(), INDEX_NAME);
                WriteToLogFile(string.Format("Saving {0} search records to index {1}", searchRecords.Count(), INDEX_NAME));

                var batches = SplitList(searchRecords, ElasticSearch_BATCH_SIZE);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    var bulkAll = elasticClient.BulkAll(batch, r => r
                        .Index(INDEX_NAME)
                        .BackOffRetries(2)
                        .BackOffTime(TimeSpan.FromSeconds(10))
                        .MaxDegreeOfParallelism(4)
                        .Size(ElasticSearch_BATCH_SIZE));

                    bulkAll.Wait(TimeSpan.FromMinutes(60), r =>
                    {
                        batchIndex++;
                        Console.WriteLine($"Data chunk {batchIndex} of {batches.Count()} indexed with {r.Items.Count} items");
                        WriteToLogFile($"Data chunk {batchIndex} of {batches.Count()} indexed with {r.Items.Count} items");
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error indexing records: {ex}");
                WriteToLogFile($"Error indexing records: {ex}");
            }
        }

        private static void UpsertRecord(SearchRecord record, ElasticClient client)
        {
            try
            {
                var searchResponse = client.Search<SearchRecord>(s => s
                    .Index(INDEX_NAME)
                    .Query(q => q
                        .Match(m => m
                            .Field(f => f.MFN)
                            .Query(record.MFN.ToString())
                        )
                    )
                );

                if (!searchResponse.IsValid || !searchResponse.Documents.Any())
                {
                    logMsg($"Record with mfn {record.MFN} not found. Trying to create new record.");

                    var createResponse = client.IndexDocument(record);
                    if (!createResponse.IsValid)
                    {
                        logMsg("Failed to create new record");
                    }
                }
                else
                {
                    var existingRecord = searchResponse.Documents.First();

                    if (record.Version > existingRecord.Version)
                    {
                        logMsg($"Record with mfn {record.MFN} found. Trying to update.");
                        var updateResponse = client.Update<SearchRecord>(existingRecord.Id, u => u
                        .Index(INDEX_NAME)
                        .Doc(record)

                    );
                        if (!updateResponse.IsValid)
                        {
                            logMsg("Failed to update record");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logMsg($"ElasticSearch error: {ex}");
            }
        }

        private static IEnumerable<IEnumerable<T>> SplitList<T>(IEnumerable<T> source, int chunkSize)
        {
            return source
                .Select((value, index) => new { Index = index, Value = value })
                .GroupBy(x => x.Index / chunkSize)
                .Select(g => g.Select(x => x.Value));
        }

        private static void WriteToLogFile(string databaseName)
        {
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine(databaseName);
            }
        }

        private static void logAndExit(string msg)
        {
            Console.WriteLine(msg);
            WriteToLogFile(msg);
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void logMsg(string msg)
        {
            Console.WriteLine(msg);
            WriteToLogFile(msg);
        }
    }
}