using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Compression;
using MathLib;
using CustomExtensionSet;

namespace ParallelArchive
{
    public class MTArchive
    {
        private object syncLock = new object();
        private string workFolderName;
        private int fileChunkSize; //1 MB

        private string sourceFileName, targetFileName;
        private DirectoryInfo workFolder;
        private FileInfo sourceFile;
        private Storage storage;

        private int optimalThreadsCount = 1;
        private int launchedThreads = 0;
        private int workThreadsCount = 0;
        private List<Thread> activeThreadsList = new List<Thread>();
        private List<WaitHandle> waitHandles = new List<WaitHandle>();
        //public List<WaitHandle> WaitHandles { get { return waitHandles; } }

        private CompressionMode workMode;
        private DateTime startTime;
        private int workResult = 1;

        /// <summary>
        /// Создаёт многопоточный архиватор с указанием рабочей папки и размера пакетов
        /// </summary>
        /// <param name="folder">Рабочая папка (если пусто, то создаётся папка Temp)</param>
        /// <param name="fileChunkSize">Размер пакета поточной обработки, МБ (1..16)</param>
        public MTArchive(string folder, int fileChunkSize)
        {
            SetWorkFolder(folder);
            SetChunkSize(fileChunkSize);
        }

        public MTArchive(string workFolder) : this(workFolder, 4)
        {

        }

        public void BeginWork(string source, string target, CompressionMode mode)
        {
            sourceFileName = source;
            targetFileName = target;
            workMode = mode;

            foreach (var file in workFolder.EnumerateFiles())
            {
                if (file.Name == sourceFileName)
                {
                    sourceFile = file;
                    break;
                }
            }

            if (sourceFile == null)
            {
                Console.WriteLine($"File \"{sourceFile}\" not found in {workFolderName} directory");
                return;
            }

            //Защищаем пользователя от попыток впихнуть невпихуемое
            if (mode == CompressionMode.Compress & sourceFile.Extension.MultipleComparsion(".gz", ".zip", ".rar"))
            {
                Console.WriteLine($"Can't compress \"*{sourceFile.Extension}\" files");
                return;
            }

            startTime = DateTime.Now;
            int filePartsPrediction = ((int)sourceFile.Length / fileChunkSize) + 1;
            optimalThreadsCount = filePartsPrediction > 2 ? Environment.ProcessorCount - 1 : filePartsPrediction;
            optimalThreadsCount = Mathl.Clamp(optimalThreadsCount, 1, Environment.ProcessorCount);
            Console.WriteLine($"Expecting {filePartsPrediction} file parts, optimal threads count is {optimalThreadsCount}");

            storage = new Storage();

            switch (mode)
            {
                case CompressionMode.Decompress:
                    Thread readingCompressed = new Thread(new ThreadStart(ReadCompressed));
                    readingCompressed.Name = "ReadingThread";
                    readingCompressed.Start();

                    for (int i = 0; i < optimalThreadsCount; i++)
                    {
                        Thread decompress = new Thread(new ThreadStart(Decompress));
                        decompress.Name = "DecompThread#" + i;
                        decompress.Start();
                    }
                    break;

                case CompressionMode.Compress:
                    Thread readingUncompressed = new Thread(new ThreadStart(ReadUncompressed));
                    readingUncompressed.Name = "ReadingThread";
                    readingUncompressed.Start();

                    targetFileName += ".pgz";

                    for (int i = 0; i < optimalThreadsCount; i++)
                    {
                        Thread compress = new Thread(new ThreadStart(Compress));
                        compress.Name = "CompThread#" + i;
                        compress.Start();
                    }
                    break;
            }

            //RunWorkThread(mode);

            Thread writingThread = new Thread(new ThreadStart(Write));
            writingThread.Name = "WritingThread";
            writingThread.Start();

            Thread.Sleep(100);

            WaitHandle.WaitAll(waitHandles.ToArray());
            
            Console.WriteLine("All threads has finished their work");
        }

        void RunWorkThread(CompressionMode mode)
        {
            lock (syncLock)
            {
                workThreadsCount++;
            }
            switch (mode)
            {
                case CompressionMode.Compress:
                    Thread compress = new Thread(new ThreadStart(Compress));
                    compress.Name = "CompThread#" + workThreadsCount;
                    compress.Start();
                    Console.WriteLine($"{compress.Name} started");
                    break;

                case CompressionMode.Decompress:
                    Thread decompress = new Thread(new ThreadStart(Decompress));
                    decompress.Name = "DecompThread#" + workThreadsCount;
                    decompress.Start();
                    Console.WriteLine($"{decompress.Name} started");
                    break;
            }
        }

        void ReadUncompressed()
        {
            AutoResetEvent wh = CountThreads();
            int chunkIndex = 0;
            try
            {
                using (FileStream sourceStream = File.OpenRead(sourceFile.FullName))
                {
                    while (sourceStream.Position < sourceStream.Length)
                    {
                        while (storage.WriteQueueSize >= optimalThreadsCount)
                        {
                            Console.WriteLine($"{storage.WriteQueueSize} chunks in readingQueue, ReadingThread is waiting");
                            Thread.Sleep(300);
                        }

                        //Определяем кол-во байт для считывания
                        int remainingFile = (int)(sourceStream.Length - sourceStream.Position);
                        int bytesToRead = remainingFile < fileChunkSize ? remainingFile : fileChunkSize;

                        //Читаем из файла
                        byte[] readData = new byte[bytesToRead];
                        sourceStream.Read(readData, 0, bytesToRead);

                        //Отправляем в хранилище для обработки
                        DataChunk uncompressedChunk = new DataChunk(readData, chunkIndex++);
                        storage.AddRawChunk(uncompressedChunk);
                        Console.WriteLine($"Chunk #{uncompressedChunk.Index} read and prepared to compression");

                        if (sourceStream.Position == sourceFile.Length)
                        {
                            Console.WriteLine("Source file read completed");
                        }
                    }
                }
                //Отправляем сигнал об окончании считывания данных
                storage.ReadingFinished();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError in reading thread: {ex.ToString()}");
            }
            finally
            {
                ThreadFinished();
                wh.Set();
                //wh.Dispose();
            }
        }

        void ReadCompressed()
        {
            AutoResetEvent wh = CountThreads();
            int chunkIndex = 0;
            try
            {
                using (FileStream sourceStream = File.OpenRead(sourceFile.FullName))
                {
                    while (sourceStream.Position < sourceStream.Length)
                    {
                        while (storage.WriteQueueSize >= optimalThreadsCount)
                        {
                            Console.WriteLine($"{storage.WriteQueueSize} chunks in readingQueue, ReadingThread is waiting");
                            Thread.Sleep(300);
                        }

                        //Считываем маркеры
                        byte[] markers = new byte[8];
                        sourceStream.Read(markers, 0, 8);
                        Console.WriteLine($"Reading markers, stream position is {sourceStream.Position}");

                        //Раскодируем и получаем длину пакета и оригинальный размер
                        int partLength = BitConverter.ToInt32(markers, 0);
                        int uncompressedLength = BitConverter.ToInt32(markers, 4);
                        Console.WriteLine($"\n>>Markers from compressed file: part length: {partLength}, original length: {uncompressedLength}");

                        //Читаем сжатые данные
                        byte[] compressedData = new byte[partLength];
                        sourceStream.Read(compressedData, 0, partLength);
                        Console.WriteLine($"Reading data, stream position is {sourceStream.Position}");

                        //Отправляем в хранилище
                        DataChunk compressedChunk = new DataChunk(compressedData, chunkIndex++, uncompressedLength, false);
                        storage.AddRawChunk(compressedChunk);
                        Console.WriteLine($"Chunk #{compressedChunk.Index} read and prepared to decompression");

                        if (sourceStream.Position == sourceFile.Length)
                        {
                            Console.WriteLine("Source file read completed");
                        }
                    }
                }

                //Отправляем сигнал об окончании считывания данных
                storage.ReadingFinished();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError in reading thread: {ex.ToString()}");
            }
            finally
            {
                ThreadFinished();
                wh.Set();
                //wh.Dispose();
            }
        }

        void Compress()
        {
            AutoResetEvent wh = CountThreads();
            try
            {
                while (!storage.IsReadQueueFinished)
                {
                    while (storage.IsReadQueueEmpty & !storage.IsReadQueueFinished)
                    {
                        Console.WriteLine("Working thread awaiting for chunks read...");
                        Thread.Sleep(200);
                        if (storage.IsReadQueueFinished) return;
                    }
                    using (MemoryStream container = new MemoryStream())
                    {
                        using(GZipStream compressor = new GZipStream(container, CompressionMode.Compress))
                        {
                            //Забираем кусок сырого файла
                            DataChunk chunkToCompress = storage.GetRawChunk();
                            Console.WriteLine($"{Thread.CurrentThread.Name} begin work with chunk #{chunkToCompress.Index}");
                            DateTime start = DateTime.Now;

                            compressor.Write(chunkToCompress.Data, 0, chunkToCompress.Data.Length);

                            //Получаем сжатый блок из потока-контейнера
                            byte[] compressedData = new byte[container.Length];
                            container.Read(compressedData, 0, compressedData.Length);

                            //Отправляем данные на запись, снабдив маркером длины блока и длины исходных данных
                            DataChunk compressedChunk = new DataChunk(compressedData, chunkToCompress.Index, chunkToCompress.Data.Length, true);
                            storage.AddChunkToWrite(compressedChunk);

                            Console.WriteLine($"{Thread.CurrentThread.Name} finished with chunk #{chunkToCompress.Index} in {(DateTime.Now - start).TotalMilliseconds}ms" +
                                $"\nCompresion level: {1f -(compressedChunk.Data.Length / chunkToCompress.Data.Length)}%");
                        }
                    }
                    //Автоматическая регулировка количества рабочих потоков
                    //if (storage.ReadQueueSize > 1 & workThreadsCount < optimalThreadsCount - 1) RunWorkThread(CompressionMode.Compress);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError in {Thread.CurrentThread.Name}: {ex.ToString()}");
            }
            finally
            {
                ThreadFinished();
                wh.Set();
                //wh.Dispose();
                lock (syncLock)
                {
                    workThreadsCount--;
                }
            }
        }

        void Decompress()
        {
            AutoResetEvent wh = CountThreads();
            try
            {
                while (!storage.IsReadQueueFinished)
                {
                    while (storage.IsReadQueueEmpty & !storage.IsReadQueueFinished)
                    {
                        Console.WriteLine("Working thread awaiting for chunks read...");
                        Thread.Sleep(200);
                        if (storage.IsReadQueueFinished) return;
                    }

                    using (MemoryStream container = new MemoryStream())
                    {
                        using(GZipStream decompressor = new GZipStream(container, CompressionMode.Decompress))
                        {
                            DataChunk compressedChunk = storage.GetRawChunk();
                            DateTime start = DateTime.Now;
                            container.Write(compressedChunk.Data, 0, compressedChunk.Data.Length);
                            Console.WriteLine($"{Thread.CurrentThread.Name} write chunk to initial stream");
                            byte[] decompressedData = new byte[compressedChunk.OriginalLength];

                            decompressor.Read(decompressedData, 0, decompressedData.Length);
                            Console.WriteLine($"{Thread.CurrentThread.Name} read decompressed data to output array");
                            DataChunk decompressedChunk = new DataChunk(decompressedData, compressedChunk.Index);

                            Console.WriteLine($"{Thread.CurrentThread.Name} sending decompressed data to storage...");
                            storage.AddChunkToWrite(decompressedChunk);
                            Console.WriteLine($"{Thread.CurrentThread.Name} finished with chunk #{compressedChunk.Index} in {(DateTime.Now - start).TotalMilliseconds}ms");
                        }
                    }

                    //Автоматическая регулировка количества рабочих потоков
                    //if (storage.ReadQueueSize > 1 & workThreadsCount < optimalThreadsCount) RunWorkThread(CompressionMode.Decompress);
                } 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError in {Thread.CurrentThread.Name}: {ex.ToString()}");
            }
            finally
            {
                ThreadFinished();
                wh.Set();
                //wh.Dispose();
                lock (syncLock)
                {
                    workThreadsCount--;
                }
            }
        }

        void Write()
        {
            AutoResetEvent wh = CountThreads();
            long resultFileLength = 0;
            try
            {
                using(FileStream writingStream = File.Create(workFolder.FullName + "/" + targetFileName))
                {
                    while (!storage.IsWriteQueueFinished)
                    {
                        while (storage.IsWriteQueueEmpty & !storage.IsWriteQueueFinished)
                        {
                            //Console.WriteLine($"Writing thread awaiting for chunks to write");
                            Console.WriteLine($"WriteQueue contains {storage.WriteQueueSize} chunks");
                            //    $"\nWQempty:{storage.IsWriteQueueEmpty} WQfinished:{storage.IsWriteQueueFinished}");
                            Thread.Sleep(500);
                            if (storage.WriteQueueSize > 0) break;
                        }
                        DataChunk nextToWrite = storage.GetChunkToWrite();
                        Console.WriteLine($"Chunk {nextToWrite.Index} prepared to write");
                        writingStream.Write(nextToWrite.Data, 0, nextToWrite.Data.Length);
                    }

                    resultFileLength = writingStream.Position;
                    workResult = 0;
                    Console.WriteLine($"File {workMode} finished for {(DateTime.Now - startTime).TotalSeconds}s" +
                    $"\nInitial size is {sourceFile.Length}, final size is {resultFileLength}, compression rate {1f - (resultFileLength / sourceFile.Length)}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError in {Thread.CurrentThread.Name}: {ex.ToString()}");
            }
            finally
            {
                
                ThreadFinished();
                wh.Set();
                //wh.Dispose();
            }
        }

        AutoResetEvent CountThreads()
        {
            lock (syncLock)
            {
                activeThreadsList.Add(Thread.CurrentThread);
                launchedThreads++;
                AutoResetEvent wh = new AutoResetEvent(false);
                waitHandles.Add(wh);
                Console.WriteLine(Thread.CurrentThread.Name + " started");
                return wh;
            }
        }

        void ThreadFinished()
        {
            lock (syncLock)
            {
                activeThreadsList.Remove(Thread.CurrentThread);
                launchedThreads--;
            }
            Console.WriteLine($"{Thread.CurrentThread.Name} is finished and stopped");
        }

        public int WorkResult()
        {
            return workResult;
        }

        public void SetChunkSize(int size)
        {
            fileChunkSize = Mathl.Clamp(size, 1, 16) * 1048576;
        }

        public void SetWorkFolder(string folder)
        {
            workFolderName = folder.Length > 0 ? folder : "Temp";

            if (Directory.Exists(workFolderName))
            {
                this.workFolder = new DirectoryInfo(workFolderName);
            }
            else
            {
                this.workFolder = Directory.CreateDirectory(workFolderName);
            }
        }
    }
}
