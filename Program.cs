#define USE_VOLATILE_OPS

using System;
using System.Threading;
using System.Runtime.CompilerServices;


namespace StoreLoad
{
    public class Program
    {
#if USE_VOLATILE_DECLS
        private static volatile int x0, x1;
        private static volatile int r0, r1;
#else
        private static int x0, x1;
        private static int r0, r1;
#endif

        private static int firstThreadRunCount;
        private static int secondThreadRunCount;
        private static Barrier syncBarrier = new Barrier(3);
        private static Action maybeMemoryFence;
        private static int _cmpxchgDummy;

        private enum MemoryFenceType 
        {
            None,
            MemoryBarrier,
            CompareExchange
        }

        private static Action GetMemoryFence(MemoryFenceType fenceType)
        {
            switch(fenceType)
            {
                case MemoryFenceType.None:
                    return () => {};
                case MemoryFenceType.MemoryBarrier:
                    return Interlocked.MemoryBarrier;
                case MemoryFenceType.CompareExchange:
                    return () => {Interlocked.CompareExchange(ref _cmpxchgDummy, 0, 1);};               
            }
            throw new ArgumentOutOfRangeException(nameof(fenceType));
        }

        public static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("Usage: <number of iterations> <memory fence type>");
                Console.WriteLine("Valid memory fence types: None, MemoryBarrier, CompareExchange");
                return;
            }
            var memoryFenceType = (MemoryFenceType) Enum.Parse(typeof(MemoryFenceType), args[1]);
            maybeMemoryFence = GetMemoryFence(memoryFenceType);

            var firstThread = new Thread(FirstThreadFunction){IsBackground = true};
            var secondThread = new Thread(SecondThreadFunction){IsBackground = true};
            
            firstThread.Start();
            secondThread.Start();
            int numIterations = Int32.Parse(args[0]);
            int numNonSeqCst = 0;
            
            for(int i = 0; i < numIterations; ++i)
            {
#if USE_VOLATILE_OPS
                Volatile.Write(ref x0, 0);
                Volatile.Write(ref x1, 0);
#else
                x0 = x1 = 0;
#endif
                syncBarrier.SignalAndWait();
                syncBarrier.SignalAndWait();
                if(firstThreadRunCount != secondThreadRunCount)
                {
                    Console.WriteLine($"Self-test failure: Both threads did not run a full iteration {firstThreadRunCount}/{secondThreadRunCount}");
                    return;
                } 
                if(x0 != 1 || x1 != 1)
                {
                    Console.WriteLine($"Self-test failure: x0 = {x0}, x1 = {x1}");
                    return;
                }               
#if USE_VOLATILE_OPS
                if(Volatile.Read(ref r0) == 0 && Volatile.Read(ref r1) == 0)
#else
                if(r0 == 0 && r1 == 0) // Check for a re-ordering.
#endif                
                {
                    ++numNonSeqCst;
                    Console.WriteLine($"StoreLoad re-ordering #{numNonSeqCst} detected after {i + 1} iterations ({firstThreadRunCount}/{secondThreadRunCount}): x0 = {x0}, x1 = {x1}");
                }
            }
            Console.WriteLine();
      
            Console.WriteLine($"{numNonSeqCst} of {numIterations} ({firstThreadRunCount}/{secondThreadRunCount}) iterations were not sequentially consistent.");
            var usingMemoryFences = memoryFenceType != MemoryFenceType.None;
            if(numNonSeqCst > 0)
            {
                Console.WriteLine($"That's about 1 in every " + numIterations / numNonSeqCst + " executions or " + 100 * numNonSeqCst / (float) numIterations + "%");
                if(usingMemoryFences)
                {
                    Console.WriteLine($"That seems wrong as you're using memory fences ({memoryFenceType})");
                }
                else
                {
                    Console.WriteLine("And that's OK, because you weren't using memory fences.");
                }
            }
            else if(usingMemoryFences)
            {
                Console.WriteLine("And that's to be expected, because you were using memory fences.");
            }
        }
        [MethodImplAttribute(MethodImplOptions.NoOptimization)]
        private static void FirstThreadFunction()
        {
            while(true)
            {
                syncBarrier.SignalAndWait();
#if USE_VOLATILE_OPS
                Volatile.Write(ref x0, 1);
                maybeMemoryFence();
                Volatile.Write(ref r0, Volatile.Read(ref x1));
#else
                x0 = 1;
                maybeMemoryFence();
                r0 = x1;
#endif
                Interlocked.Increment(ref firstThreadRunCount);
                syncBarrier.SignalAndWait();    
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoOptimization)]
        private static void SecondThreadFunction()
        {
            while(true)
            {
                syncBarrier.SignalAndWait();
#if USE_VOLATILE_OPS
                Volatile.Write(ref x1, 1);
                maybeMemoryFence();
                Volatile.Write(ref r1, Volatile.Read(ref x0));
#else
                x1 = 1;
                maybeMemoryFence();
                r1 = x0;
#endif
                Interlocked.Increment(ref secondThreadRunCount);
                syncBarrier.SignalAndWait();
            }
        }
    }
}
