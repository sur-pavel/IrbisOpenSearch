using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace IrbisOpensearch
{
    internal struct DocumentFileLeader
    {
        public int MasterFileNumber;
        public int MasterFileRecordLength;
        public int MasterFileBlockLow;
        public int MasterFileBlockHigh;
        public int BaseAddress;
        public int NumberOfVariableFields;
        public int Status;
        public int Version;
    }

    internal struct DocumentFileDictionaryEntry
    {
        public int Tag;
        public int Position;
        public int Length;
    }

    internal struct ControlRecord
    {
        public int ControlMasterFileNumber;
        public int NextMasterFileNumber;
        public int NextMasterFileBlockLow;
        public int NextMasterFileBlockHigh;
        public int MasterFileType;
        public int RecordCount;
        public int ControlField1;
        public int ControlField2;
        public int ControlField3;
    }

    internal class MstReader
    {
        public static void Read(string fileName)
        {
            int numThreads = Environment.ProcessorCount; // количество потоков равно количеству ядер процессора

            try
            {
                MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileName);
                DocumentFileLeader leader = new DocumentFileLeader();
                List<DocumentFileDictionaryEntry> dictionary = new List<DocumentFileDictionaryEntry>();
                List<byte[]> variableFields = new List<byte[]>();
                ControlRecord controlRecord = new ControlRecord();

                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, Marshal.SizeOf(typeof(DocumentFileLeader)), MemoryMappedFileAccess.Read))
                {
                    accessor.Read<DocumentFileLeader>(0, out leader);
                }

                int dictionaryLength = 12 * leader.NumberOfVariableFields;
                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(Marshal.SizeOf(typeof(DocumentFileLeader)), dictionaryLength, MemoryMappedFileAccess.Read))
                {
                    for (int i = 0; i < leader.NumberOfVariableFields; i++)
                    {
                        DocumentFileDictionaryEntry entry;
                        accessor.Read<DocumentFileDictionaryEntry>(i * 12, out entry);
                        dictionary.Add(entry);
                    }
                }

                int variableFieldsOffset = leader.BaseAddress;
                for (int i = 0; i < numThreads; i++)
                {
                    int chunkSize = (int)Math.Ceiling((double)(dictionaryLength + leader.MasterFileRecordLength * leader.MasterFileBlockLow) / numThreads);
                    int chunkOffset = Marshal.SizeOf(typeof(DocumentFileLeader)) + dictionaryLength + i * chunkSize;
                    int chunkLength = Math.Min(chunkSize, (int)(chunkOffset));
                    //int chunkLength = Math.Min(chunkSize, (int)(mmf.SafeMemoryMappedFileHandle.ByteLength - chunkOffset));

                    Thread thread = new Thread(async () =>
                    {
                        using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(chunkOffset, chunkLength, MemoryMappedFileAccess.Read))
                        {
                            for (int j = 0; j < leader.MasterFileBlockLow; j++)
                            {
                                byte[] fieldData = new byte[leader.MasterFileRecordLength];
                                //await accessor.ReadArrayAsync<byte>(variableFieldsOffset + j * leader.MasterFileRecordLength, fieldData, 0, leader.MasterFileRecordLength);
                                variableFields.Add(fieldData);
                            }
                        }
                    });

                    thread.Start();
                }

                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(Marshal.SizeOf(typeof(DocumentFileLeader)) + dictionaryLength + leader.MasterFileRecordLength * leader.MasterFileBlockLow, Marshal.SizeOf(typeof(DocumentFileLeader)), MemoryMappedFileAccess.Read))
                {
                    accessor.Read<ControlRecord>(0, out controlRecord);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }
    }
}