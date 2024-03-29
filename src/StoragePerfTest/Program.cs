﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Storage;
using ECommon.Storage.FileNamingStrategies;
using ECommon.Utilities;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace StoragePerfTest
{
    class Program
    {
        static void Main(string[] args)
        {
            WriteChunkPerfTest();

            //ReadIndexChunkTest(WriteIndexChunkTest());
            //ReadChunkTest(WriteChunkTest());
        }

        static ChunkManagerConfig BuildIndexChunkConfiguration()
        {
            var storeRootPath = ConfigurationManager.AppSettings["storeRootPath"];                                          //文件存储根目录
            var threadCount = int.Parse(ConfigurationManager.AppSettings["concurrentThreadCount"]);                         //并行写数据的线程数
            var recordSize = int.Parse(ConfigurationManager.AppSettings["recordSize"]);                                     //记录大小，字节为单位
            var recordCount = int.Parse(ConfigurationManager.AppSettings["recordCount"]);                                   //总共要写入的记录数
            var syncFlush = bool.Parse(ConfigurationManager.AppSettings["syncFlush"]);                                      //是否同步刷盘
            FlushOption flushOption;
            Enum.TryParse(ConfigurationManager.AppSettings["flushOption"], out flushOption);                                //同步刷盘方式
            var chunkSize = 1024 * 1024 * 100;
            var enableCache = true;
            var flushInterval = 100;
            var maxRecordSize = 5 * 1024 * 1024;
            var chunkWriteBuffer = 128 * 1024;
            var chunkReadBuffer = 128 * 1024;
            return new ChunkManagerConfig(
                Path.Combine(storeRootPath, @"message-chunks"),
                new DefaultFileNamingStrategy("message-chunk-"),
                ChunkType.Index,
                100,
                chunkSize,
                0,
                0,
                flushInterval,
                enableCache,
                syncFlush,
                flushOption,
                Environment.ProcessorCount * 8,
                maxRecordSize,
                chunkWriteBuffer,
                chunkReadBuffer,
                5,
                1,
                1,
                5,
                10 * 10000,
                true);
        }
        static ChunkManagerConfig BuildChunkConfiguration()
        {
            var storeRootPath = ConfigurationManager.AppSettings["storeRootPath"];                                          //文件存储根目录
            var threadCount = int.Parse(ConfigurationManager.AppSettings["concurrentThreadCount"]);                         //并行写数据的线程数
            var recordSize = int.Parse(ConfigurationManager.AppSettings["recordSize"]);                                     //记录大小，字节为单位
            var recordCount = int.Parse(ConfigurationManager.AppSettings["recordCount"]);                                   //总共要写入的记录数
            var syncFlush = bool.Parse(ConfigurationManager.AppSettings["syncFlush"]);                                      //是否同步刷盘
            FlushOption flushOption;
            Enum.TryParse(ConfigurationManager.AppSettings["flushOption"], out flushOption);                                //同步刷盘方式
            var chunkSize = 1024 * 1024 * 100;
            var enableCache = true;
            var flushInterval = 100;
            var maxRecordSize = 5 * 1024 * 1024;
            var chunkWriteBuffer = 128 * 1024;
            var chunkReadBuffer = 128 * 1024;
            return new ChunkManagerConfig(
                Path.Combine(storeRootPath, @"message-chunks"),
                new DefaultFileNamingStrategy("message-chunk-"),
                ChunkType.Default,
                0,
                chunkSize,
                0,
                0,
                flushInterval,
                enableCache,
                syncFlush,
                flushOption,
                Environment.ProcessorCount * 8,
                maxRecordSize,
                chunkWriteBuffer,
                chunkReadBuffer,
                5,
                1,
                1,
                5,
                10 * 10000,
                true);
        }

        static IList<long> WriteIndexChunkTest()
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseSerilog()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            Directory.Delete(Path.Combine(ConfigurationManager.AppSettings["storeRootPath"], @"message-chunks"), true);

            var chunkManager = new ChunkManager("message-chunk", BuildIndexChunkConfiguration(), false);
            var chunkWriter = new ChunkWriter(chunkManager);

            chunkManager.Load(ReadRecord);
            chunkWriter.Open();
            chunkWriter.CurrentChunk.WriteBloomFilter("1", "9", Encoding.UTF8.GetBytes("123456789"));
            var positionList = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                positionList.Add(chunkWriter.Write(new BufferLogRecord
                {
                    RecordBuffer = Encoding.UTF8.GetBytes("message-" + i.ToString())
                }));
                Console.WriteLine("message-" + i);
            }
            chunkWriter.Close();
            chunkManager.Close();

            return positionList;
        }
        static void ReadIndexChunkTest(IList<long> positionList)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseSerilog()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            var chunkManager = new ChunkManager("message-chunk", BuildIndexChunkConfiguration(), false);
            chunkManager.Load(ReadRecord);

            Chunk chunk = chunkManager.GetFirstChunk();
            Console.WriteLine("ChunkBloomFilter size: " + chunk.ChunkBloomFilter.Size);
            Console.WriteLine("ChunkBloomFilter minKey: " + chunk.ChunkBloomFilter.MinKey);
            Console.WriteLine("ChunkBloomFilter maxKey: " + chunk.ChunkBloomFilter.MaxKey);
            Console.WriteLine("ChunkBloomFilter content: " + Encoding.UTF8.GetString(chunk.ChunkBloomFilter.BloomFilterBytes));

            var chunkReader = new ChunkReader(chunkManager);
            foreach (var position in positionList)
            {
                BufferLogRecord record = chunkReader.TryReadAt(position, ReadRecord);
                Console.WriteLine(Encoding.UTF8.GetString(record.RecordBuffer));
            }
            Console.ReadLine();

            chunkManager.Close();
        }

        static IList<long> WriteChunkTest()
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseSerilog()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            Directory.Delete(Path.Combine(ConfigurationManager.AppSettings["storeRootPath"], @"message-chunks"), true);

            var chunkManager = new ChunkManager("message-chunk", BuildChunkConfiguration(), false);
            var chunkWriter = new ChunkWriter(chunkManager);

            chunkManager.Load(ReadRecord);
            chunkWriter.Open();
            var positionList = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                positionList.Add(chunkWriter.Write(new BufferLogRecord
                {
                    RecordBuffer = Encoding.UTF8.GetBytes("message-" + i.ToString())
                }));
                Console.WriteLine("message-" + i);
            }
            chunkWriter.Close();
            chunkManager.Close();

            return positionList;
        }
        static void ReadChunkTest(IList<long> positionList)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseSerilog()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            var chunkManager = new ChunkManager("message-chunk", BuildChunkConfiguration(), false);
            chunkManager.Load(ReadRecord);

            var chunkReader = new ChunkReader(chunkManager);
            foreach (var position in positionList)
            {
                BufferLogRecord record = chunkReader.TryReadAt(position, ReadRecord);
                Console.WriteLine(Encoding.UTF8.GetString(record.RecordBuffer));
            }
            Console.ReadLine();

            chunkManager.Close();
        }

        static void WriteChunkPerfTest()
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseSerilog()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            var storeRootPath = ConfigurationManager.AppSettings["storeRootPath"];                                          //文件存储根目录
            var threadCount = int.Parse(ConfigurationManager.AppSettings["concurrentThreadCount"]);                         //并行写数据的线程数
            var recordSize = int.Parse(ConfigurationManager.AppSettings["recordSize"]);                                     //记录大小，字节为单位
            var recordCount = int.Parse(ConfigurationManager.AppSettings["recordCount"]);                                   //总共要写入的记录数
            var syncFlush = bool.Parse(ConfigurationManager.AppSettings["syncFlush"]);                                      //是否同步刷盘
            FlushOption flushOption; 
            Enum.TryParse(ConfigurationManager.AppSettings["flushOption"], out flushOption);                                //同步刷盘方式
            var chunkSize = 1024 * 1024 * 1024;
            var flushInterval = 100;
            var maxRecordSize = 5 * 1024 * 1024;
            var chunkWriteBuffer = 128 * 1024;
            var chunkReadBuffer = 128 * 1024;
            var chunkManagerConfig = new ChunkManagerConfig(
                Path.Combine(storeRootPath, @"perf-chunks"),
                new DefaultFileNamingStrategy("perf-chunk-"),
                chunkSize,
                0,
                0,
                flushInterval,
                false,
                syncFlush,
                flushOption,
                Environment.ProcessorCount * 8,
                maxRecordSize,
                chunkWriteBuffer,
                chunkReadBuffer,
                5,
                1,
                1,
                5,
                10 * 10000,
                true);
            var chunkManager = new ChunkManager("perf-chunk-manager", chunkManagerConfig, false);
            var chunkWriter = new ChunkWriter(chunkManager);
            chunkManager.Load(ReadRecord);
            chunkWriter.Open();
            var record = new BufferLogRecord
            {
                RecordBuffer = new byte[recordSize]
            };
            var count = 0L;
            var performanceService = ObjectContainer.Resolve<IPerformanceService>();
            performanceService.Initialize("WriteChunk").Start();

            for (var i = 0; i < threadCount; i++)
            {
                Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        var current = Interlocked.Increment(ref count);
                        if (current > recordCount)
                        {
                            break;
                        }
                        var start = DateTime.Now;
                        chunkWriter.Write(record);
                        performanceService.IncrementKeyCount("default", (DateTime.Now - start).TotalMilliseconds);
                    }
                });
            }

            Console.ReadLine();

            chunkWriter.Close();
            chunkManager.Close();
        }

        static BufferLogRecord ReadRecord(byte[] recordBuffer)
        {
            var record = new BufferLogRecord();
            record.ReadFrom(recordBuffer);
            return record;
        }
        class BufferLogRecord : ILogRecord
        {
            public byte[] RecordBuffer { get; set; }

            public void WriteTo(long logPosition, BinaryWriter writer)
            {
                writer.Write(logPosition);
                writer.Write(RecordBuffer.Length);
                writer.Write(RecordBuffer);
            }
            public void ReadFrom(byte[] recordBuffer)
            {
                var srcOffset = 0;
                var logPosition = ByteUtil.DecodeLong(recordBuffer, srcOffset, out srcOffset);
                RecordBuffer = ByteUtil.DecodeBytes(recordBuffer, srcOffset, out srcOffset);
            }
        }
    }
}
