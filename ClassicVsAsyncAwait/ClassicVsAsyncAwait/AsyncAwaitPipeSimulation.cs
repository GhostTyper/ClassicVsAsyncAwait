using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicVsAsyncAwait
{
    class AsyncAwaitPipeSimulation
    {
        private TaskCompletionSource<object?>? completion;

        private object sync;

        private int segments;

        public AsyncAwaitPipeSimulation()
        {
            completion = new TaskCompletionSource<object?>();
            sync = new object();
        }

        public void Write()
        {
            lock (sync)
            {
                segments++;

                if (completion != null)
                {
                    TaskCompletionSource<object?>? tCompletion = completion;

                    completion = null;

                    ThreadPool.QueueUserWorkItem(delegate {
                        tCompletion.SetResult(null);
                    });
                }
            }
        }

        public async Task Read()
        {
            TaskCompletionSource<object?>? tCompletion;

            lock (sync)
                if (segments > 0)
                {
                    segments--;

                    return;
                }
                else
                {
                    tCompletion = new TaskCompletionSource<object?>();

                    completion = tCompletion;
                }

            await tCompletion.Task.ConfigureAwait(false);

            lock (sync)
                if (segments > 0)
                    segments--;
        }
    }
}
