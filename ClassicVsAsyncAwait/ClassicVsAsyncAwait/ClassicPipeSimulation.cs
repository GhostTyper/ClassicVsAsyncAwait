using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ClassicVsAsyncAwait
{
    class ClassicPipeSimulation : IDisposable
    {
        private AutoResetEvent are;

        private object sync;

        private int segments;

        public ClassicPipeSimulation()
        {
            are = new AutoResetEvent(false);
            sync = new object();
        }

        public void Write()
        {
            lock (sync)
            {
                segments++;

                if (segments == 1)
                    are.Set();
            }
        }

        public void Read()
        {
            lock (sync)
                if (segments > 0)
                {
                    segments--;

                    return;
                }

            while (true)
            {
                are.WaitOne();

                lock (sync)
                    if (segments > 0)
                    {
                        segments--;

                        return;
                    }
            }
        }

        public void Dispose()
        {
            are.Dispose();
        }
    }
}
