using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ParallelArchive
{
    public class Storage
    {
        private static Mutex readLock = new Mutex(), writeLock = new Mutex();
        private Queue<DataChunk> readQueue = new Queue<DataChunk>(), writeQueue = new Queue<DataChunk>();

        private int nextChunkIndexToWrite = 0;

        private int readChunksAccepted = 0;
        private int writeChunksAccepted = 0;
        private bool isAllReadChunksArrived = false;
        private bool isAllWriteChunksArrived = false;
        public bool IsAllReadChunksArrived { get { return isAllReadChunksArrived; } }
        public bool IsAllWriteChunksArrived { get { return isAllWriteChunksArrived; } }

        public int ReadQueueSize { get { return readQueue.Count; } }
        public int WriteQueueSize { get { return writeQueue.Count; } }
        public bool IsReadQueueEmpty { get { return readQueue.Count == 0; } }
        public bool IsWriteQueueEmpty { get { return writeQueue.Count == 0; } }
        public bool IsReadQueueFinished { get { return IsReadQueueEmpty & isAllReadChunksArrived; } }
        public bool IsWriteQueueFinished { get { return IsWriteQueueEmpty & isAllWriteChunksArrived; } }

        //Постановка в очередь необработанных пакетов
        public void AddRawChunk(DataChunk chunk)
        {
            lock (readLock)
            {
                readQueue.Enqueue(chunk);
                readChunksAccepted++;
                Monitor.Pulse(readLock);
            }
            Console.WriteLine($"\n>>Got chunk #{chunk.Index} in readQueue, {readQueue.Count} chunks waiting in queue");
        }

        //Постановка в очередь сжатых/распакованных пакетов
        public void AddChunkToWrite(DataChunk chunk)
        {
            lock (writeLock)
            {
                Console.WriteLine($"{Thread.CurrentThread.Name} trying to enqueue chunk #{chunk.Index}");

                //Проверка очерёдности поступления пакетов
                while (chunk.Index != nextChunkIndexToWrite)
                {
                    Console.WriteLine($"{Thread.CurrentThread.Name} waiting for previous chunks...");
                    Monitor.Wait(writeLock);
                }
                writeQueue.Enqueue(chunk);
                writeChunksAccepted++;
                nextChunkIndexToWrite++;
                if (isAllReadChunksArrived & readChunksAccepted == writeChunksAccepted)
                {
                    isAllWriteChunksArrived = true;
                    Console.WriteLine("\nGot last chunk in writeQueue");
                }
                Console.WriteLine($"\n>>Got chunk #{chunk.Index} in writeQueue, {writeQueue.Count} chunks waiting in queue");
                Monitor.PulseAll(writeLock);
                
                //writeLock.ReleaseMutex();
            }
        }

        //Получение данных из оригинального файла 
        public DataChunk GetRawChunk()
        {
            lock (readLock)
            {
                if (IsReadQueueEmpty)
                {
                    readLock.WaitOne();
                }
                DataChunk chunk = readQueue.Dequeue();
                Console.WriteLine($"<<Dequeued chunk #{chunk.Index} from readQueue, {readQueue.Count} remained");
                return chunk;
            }
        }

        //Получение данных на запись в готовый файл
        public DataChunk GetChunkToWrite()
        {
            lock (writeLock)
            {
                if (IsWriteQueueEmpty)
                {
                    writeLock.WaitOne();
                }
                DataChunk chunk = writeQueue.Dequeue();
                Console.WriteLine($"<<Dequeued chunk #{chunk.Index} from writeQueue, {writeQueue.Count} remained");
                return chunk;
            }
        }

        public void ReadingFinished()
        {
            //Сигнал об окончании чтения
            isAllReadChunksArrived = true;
        }
    }

    public class DataChunk
    {
        private byte[] data;
        private int index;
        private int originalLength;

        public byte[] Data { get { return data; } }
        public int Index { get { return index; } }
        public int OriginalLength { get { return originalLength; } }

        /// <summary>
        /// Создаёт экземпляр DataChunk
        /// </summary>
        /// <param name="data">Блок данных</param>
        /// <param name="index">Номер блока</param>
        /// <param name="uncompressedDataLength">Длина несжатого блока</param>
        /// <param name="encodeLengthToData">Кодирование маркеров в начало блока</param>
        public DataChunk(byte[] data, int index, int uncompressedDataLength, bool encodeLengthToData)
        {
            if (encodeLengthToData)
            {
                //Добавляем маркеры длины сжатых и несжатых (оригинальных) данных
                this.data = new byte[8 + data.Length];
                BitConverter.GetBytes(data.Length).CopyTo(this.data, 0);
                BitConverter.GetBytes(uncompressedDataLength).CopyTo(this.data, 4);
                Console.WriteLine($"Package markers: data length: {data.Length}, uncopressed length: {uncompressedDataLength}");
                data.CopyTo(this.data, 8);
            }
            else
            {
                this.data = data;
                this.originalLength = uncompressedDataLength;
            }                   
            this.index = index;
        }

        public DataChunk(byte[] data, int index) : this(data, index, 0, false)
        {

        }
    }
}
