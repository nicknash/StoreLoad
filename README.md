# StoreLoad

This repo contains a fairly minimal example of the famous "store-load" memory re-ordering of x86/x64 processors, in C#.

As a very brief summary, x86/x64 architectures have a "strong" memory model, that is, in multi CPU systems they don't allow most re-orderings of loads and stores from program order to memory order.

In particular: 

1. Loads aren't re-ordered with other loads. 'LoadLoad' 
2. Stores aren't re-ordered with other stores. 'StoreStore' 
3. Stores aren't re-ordered with older loads. 'LoadStore' 

But!

Loads **may** be re-ordered with older stores. 'StoreLoad'

This last point has a funny implication, which is pointed out just about everywhere you see the x86/x64 memory model discussed. If two threads run on different processors, and the first thread does this:

```csharp
x0 = 1;
r0 = x1;
```

and the second thread does this:

```csharp
x1 = 1;
r1 = x0;
```

Then, if x0 and x1 are both initially zero, after both threads are finished, it's possible to observe r0 = r1 = 0.
This is (maybe) surprising, as this execution is not **sequentially consistent**, i.e., there's no way to take the instructions of the first and second thread and interleave them somehow to get to this result. Instead, we'd have to re-order the store (i.e., x_i = 1) with the load (i.e., r_i = x_i).

Now, C# has a fairly strong memory model itself, but it doesn't rule out the JIT itself performing a store-load re-ordering. I've tried to rule out the JIT getting involved by specifying MethodImplOptions.NoOptimization on the methods performing the important assignments to x_i and r_i. I've never seen it officially documented that this rules out compiler re-orderings, but my experience is that it does.

The full code is [here](Program.cs)

## Example run

Here's what happens when I run the code in this repo on my quad-core Mac Book:
```
➜  StoreLoad git:(master) ✗ dotnet run 50000 
Project StoreLoad (.NETCoreApp,Version=v1.0) was previously compiled. Skipping compilation.
StoreLoad re-ordering #1 detected after 3797 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #2 detected after 4282 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #3 detected after 7535 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #4 detected after 11125 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #5 detected after 14246 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #6 detected after 28248 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #7 detected after 31277 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #8 detected after 42945 iterations: x0 = 1, x1 = 1
StoreLoad re-ordering #9 detected after 44868 iterations: x0 = 1, x1 = 1

9 of 50000 were not sequentially consistent.
That's about 1 in every 5555 executions or 0.018%
And that's OK, because you weren't using memory barriers.
```

### Why does this happen?

It depends how many times you ask why I suppose. The first answer is that x86/64 CPUs have FIFO store-buffers, so before values are written to cache/memory they're written to a small buffer. While in there, the stores are visible to that processor (they're 'forwarded' to other loads on that processor - as they have to be to keep single threaded code correct) but are not visible to other processors.

A memory barrier instruction can be used to wait for store buffers to drain. Including a memory barrier when running [the code](Program.cs) yields this:

```
➜  StoreLoad git:(master) ✗ dotnet run 50000 m
Project StoreLoad (.NETCoreApp,Version=v1.0) was previously compiled. Skipping compilation.

0 of 50000 were not sequentially consistent.
And that's to be expected, because you were using memory barriers.
```

## Other Intel re-ordering rules

Intel have a [bunch](Intel64MemoryOrdering.pdf) of rules about how loads and stores behave, it'd probably be a good exercise to implement similar test programs for these, although not terribly interesting. 

1. Loads are not reordered with other loads.
2. Stores are not reordered with other stores.
3. Stores are not reordered with older loads.
4. Loads may be reordered with older stores to different locations but not with older stores to the same location.
5. In a multiprocessor system, memory ordering obeys causality (memory ordering respects transitive visibility).
6. In a multiprocessor system, stores to the same location have a total order.
7. In a multiprocessor system, locked instructions have a total order.
8. Loads and stores are not reordered with locked instructions.

## C# Memory Model

C#, as far as I know, doesn't have a carefully documented memory model. Joe Duffy's blog is my source for its rules:

1. Data dependence among loads and stores is never violated.
2. All stores have release semantics, i.e. no load or store may move after one.
3. All volatile loads are acquire, i.e. no load or store may move before one.
4. No loads and stores may ever cross a full-barrier (e.g. Interlocked.MemoryBarrier, lock acquire, Interlocked.Exchange, Interlocked.CompareExchange, etc.).
5. Loads and stores to the heap may never be introduced.
6. Loads and stores may only be deleted when coalescing adjacent loads and stores from/to the same location.

## Further Reading

1. Jeff Preshing has a lovely long illustration of this in C++ on his [http://preshing.com/20120515/memory-reordering-caught-in-the-act/](superb blog). He also investigates setting thread affinity to make the effect disappear (something I can't do as .NET Core doesn't currently support this). The comments on that post are also great. I tried cache-line padding the fields as one of the comments there suggests, but didn't see a dramatic change in how prevelant re-orderings were.

2. Joe Duffy's blog post on the C# memory model: http://joeduffyblog.com/2007/11/10/clr-20-memory-model/ 
