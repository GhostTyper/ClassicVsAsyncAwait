using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicVsAsyncAwait
{
    class Program
    {
        private static int threads;

        private static object threadSync = new object();

        private static long[] counters = new long[0];
        private static object[] syncs = new object[0];

        private static ManualResetEvent mre = new ManualResetEvent(false);
        private static bool runReaders;
        private static bool runWriters;

        static void Main()
        {
            Thread.Sleep(15000); // I just close things like the IDE for a real test...

            long[,] asyncRun = new long[13, 6];
            long[,] classicRun = new long[13, 6];

            for (int readers = 0; readers < 13; readers++)
                for (int writers = 0; writers < 6; writers++)
                {
                    asyncRun[readers, writers] = AsyncTestRun(1 << (readers + 5), 1 << (writers));
                    Cleanup();
                }

            for (int readers = 0; readers < 13; readers++)
                for (int writers = 0; writers < 6; writers++)
                {
                    classicRun[readers, writers] = ClassicTestRun(1 << (readers + 5), 1 << (writers));
                    Cleanup();
                }

            Console.WriteLine();
            Console.WriteLine("Classic:");
            Console.WriteLine();

            Console.WriteLine("Readers\\Writers;1;2;4;8;16;32");

            for (int readers = 0; readers < 13; readers++)
            {
                Console.Write(1 << (readers + 5));

                for (int writers = 0; writers < 6; writers++)
                    Console.Write($";{classicRun[readers, writers]}");

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("Async:");
            Console.WriteLine();

            Console.WriteLine("Readers\\Writers;1;2;4;8;16;32");

            for (int readers = 0; readers < 13; readers++)
            {
                Console.Write(1 << (readers + 5));

                for (int writers = 0; writers < 6; writers++)
                    Console.Write($";{asyncRun[readers, writers]}");

                Console.WriteLine();
            }
        }

        static void Cleanup()
        {
            GC.Collect();

            Thread.Sleep(5000);
        }

        static long ClassicTestRun(int readThreads, int writeThreads)
        {
            Console.Write($" * ClassicTestRun({readThreads}, {writeThreads}): CRTHD");

            counters = new long[writeThreads];
            syncs = new object[writeThreads];

            int threadsPerWriter = readThreads / writeThreads;

            threads = threadsPerWriter * writeThreads;

            for (int writer = 0; writer < writeThreads; writer++)
            {
                syncs[writer] = new object();

                List<ClassicPipeSimulation> clients = new List<ClassicPipeSimulation>();

                Thread thd = new Thread(new ParameterizedThreadStart(writeClassic));
                thd.Start(new object?[] { clients, writer });

                for (int client = 0; client < threadsPerWriter; client++)
                {
                    ClassicPipeSimulation pipe = new ClassicPipeSimulation();

                    clients.Add(pipe);

                    thd = new Thread(new ParameterizedThreadStart(readClassic));
                    thd.Start(new object?[] { pipe, writer });
                }
            }

            Console.Write(", WARMUP");

            Thread.Sleep(1000);
            runReaders = true;
            runWriters = true;
            mre.Set();
            Thread.Sleep(30000);

            Console.Write(", START");

            for (int writer = 0; writer < writeThreads; writer++)
                lock (syncs[writer])
                    counters[writer] = 0;

            Stopwatch sw = Stopwatch.StartNew();

            Thread.Sleep(60000);

            long result = 0;

            for (int writer = 0; writer < writeThreads; writer++)
                result += counters[writer];

            sw.Stop();

            Console.Write(", CLDN");

            mre.Reset();
            runReaders = false;

            while (true)
            {
                Thread.Sleep(100);

                lock (threadSync)
                    if (threads == 0)
                        break;
            }

            runWriters = false;

            Thread.Sleep(1000);

            Console.WriteLine($": {(long)(result / sw.Elapsed.TotalMinutes + 0.5)}.");

            return (long)(result / sw.Elapsed.TotalMinutes + 0.5);
        }

        static long AsyncTestRun(int readThreads, int writeThreads)
        {
            Console.Write($" * AsyncTestRun({readThreads}, {writeThreads}): CRTHD");

            counters = new long[writeThreads];
            syncs = new object[writeThreads];

            runReaders = true;
            runWriters = true;

            int threadsPerWriter = readThreads / writeThreads;

            threads = threadsPerWriter * writeThreads;

            for (int writer = 0; writer < writeThreads; writer++)
            {
                syncs[writer] = new object();

                List<AsyncAwaitPipeSimulation> clients = new List<AsyncAwaitPipeSimulation>();

                Thread thd = new Thread(new ParameterizedThreadStart(writeAsync));
                thd.Start(new object?[] { clients, writer });

                for (int client = 0; client < threadsPerWriter; client++)
                {
                    int sWriter = writer;

                    AsyncAwaitPipeSimulation pipe = new AsyncAwaitPipeSimulation();

                    clients.Add(pipe);

                    ThreadPool.QueueUserWorkItem(async delegate { await readAsync(new object?[] { pipe, sWriter }); });
                }
            }

            Console.Write(", WARMUP");

            Thread.Sleep(30000);
            mre.Set();
            
            Thread.Sleep(30000);

            Console.Write(", START");

            for (int writer = 0; writer < writeThreads; writer++)
                lock (syncs[writer])
                    counters[writer] = 0;

            Stopwatch sw = Stopwatch.StartNew();

            Thread.Sleep(60000);

            long result = 0;

            for (int writer = 0; writer < writeThreads; writer++)
                result += counters[writer];

            sw.Stop();

            Console.Write(", CLDN");

            mre.Reset();
            runReaders = false;

            while (true)
            {
                Thread.Sleep(100);

                lock (threadSync)
                    if (threads == 0)
                        break;
            }

            runWriters = false;

            Thread.Sleep(1000);

            Console.WriteLine($": {(long)(result / sw.Elapsed.TotalMinutes + 0.5)}.");

            return (long)(result / sw.Elapsed.TotalMinutes + 0.5);
        }

        static void readClassic(object? parameter)
        {
            mre.WaitOne();

            ClassicPipeSimulation client = (ClassicPipeSimulation)((object[])parameter!)[0];
            int writerNumber = (int)((object[])parameter!)[1];

            while (runReaders)
            {
                client.Read();

                lock (syncs[writerNumber])
                    counters[writerNumber]++;
            }

            lock (threadSync)
                threads--;
        }

        static void writeClassic(object? parameter)
        {
            mre.WaitOne();

            List<ClassicPipeSimulation> clients = (List<ClassicPipeSimulation>)((object[])parameter!)[0];

            while (runWriters)
                foreach (ClassicPipeSimulation pipe in clients)
                    pipe.Write();
        }

        static async Task readAsync(object? parameter)
        {
            AsyncAwaitPipeSimulation client = (AsyncAwaitPipeSimulation)((object[])parameter!)[0];
            int writerNumber = (int)((object[])parameter!)[1];

            while (runReaders)
            {
                await client.Read();

                lock (syncs[writerNumber])
                    counters[writerNumber]++;
            }

            lock (threadSync)
                threads--;
        }

        static void writeAsync(object? parameter)
        {
            mre.WaitOne();

            List<AsyncAwaitPipeSimulation> clients = (List<AsyncAwaitPipeSimulation>)((object[])parameter!)[0];

            while (runWriters)
                foreach (AsyncAwaitPipeSimulation pipe in clients)
                    pipe.Write();
        }
    }
}
