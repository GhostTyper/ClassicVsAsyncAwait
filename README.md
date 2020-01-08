# Introduction

.NET (and therefore C#) supports `Task`s and the `async`/`await` pattern. This pattern implements a state machine automatically where all async method calls or await calls are joint points.

The question of this performance comparison is: Is `async`/`await` really faster than the classic approach of reserving one thread per "waiting" call?

# Basics

Implementations like IOCP in Windows are definitively boosting network performance. Because we don't need any thread for waiting and the kernel will give us a callback when data has been received.

However, the software we write has many internal "pipes" where threads can wait for results of other threads. We have the choice of implementing this with `ManualResetEvent`/`AutoResetEvent` and real `Thread`s or with `async`/`await` pattern.

# Test case

My test case consists of many reading threads and some writing threads. All those writing threads trigger on the reading threads to continue their work. This is wither done with `ManualResetEvent`/`AutoResetEvent` or with `TaskCompletionSource<>`

`ManualResetEvent`/`AutoResetEvent` will trigger the kernel to continue the corresponding thread. `TaskCompletionSource<>` will continue at the corresponding `await` by directly calling the corresponding section in the `async`/`await` state machine. So we also need to call `tcs.SetResult(null)` from a `ThreadPool`-`Thread`.

# Expectation

My expectation is, that `async`/`await` will be quite faster than the "classic" approach, because it should save huge amounts of RAM.

Creating a thread on Windows requires the operating system to allocate 1 MB for the stack at least. However windows will allocate the memory required for each thread when this memory is really consumed leaving us with far less memory than 1 MB per stack.

`async`/`await` on the other hand should only reserve the memory required to store the local of the current execution context which shouldt be far less than creating a full thread in the operating system.

# Test

Those are the test results: `TaskCompletionSource<>` divided by `ManualResetEvent`/`AutoResetEvent` multiplied by 100:

![Performance comparison Classic/Async*100](./percent.png)

From left to right: Parallel writing threads. From top to bottom: Parallel reading threads. Green fields indicate that `async`/`await` is faster than `ManualResetEvent`/`AutoResetEvent`. The number 200 means that `async`/`await` is as double as fast than `ManualResetEvent`/`AutoResetEvent`.

Running 2^17 threads requires 4 GB RAM on my windows system while waiting for 2^17 `async`/`await` locals only requires 750 MB of memory.

Additionally it's worth to mention that just looping with 1 reader and 1 writer is way faster without `async`/`await` pattern. (`ManualResetEvent`/`AutoResetEvent`: 355M vs `async`/`await`: 141M iterations.)

# Linux

My test on Linux failed at 16k threads because of a configured system limit. I will investigate in this later.

# Conclusion

Use `async`/`await` pattern.