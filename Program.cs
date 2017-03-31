//#define USE_VOLATILE_OPS

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
        private static AutoResetEvent first = new AutoResetEvent(false);
        private static AutoResetEvent second = new AutoResetEvent(false);
        private static Barrier barrier = new Barrier(3);

        private static Action maybeMemoryBarrier;
        public static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                Console.WriteLine("Usage: <number of iterations> [m]emory bariers on.");
                return;
            }
            bool useMemoryBarriers = args.Length >= 2;
            maybeMemoryBarrier = useMemoryBarriers ? (Action) Interlocked.MemoryBarrier : () => {};
            
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
                first.Set(); // Allow the two threads to run
                second.Set();
                barrier.SignalAndWait(); // Ensure that they both actually ran
                
#if USE_VOLATILE_OPS
                if(Volatile.Read(ref r0) == 0 && Volatile.Read(ref r1) == 0)
#else
                if(r0 == 0 && r1 == 0) // Check for a re-ordering.
#endif                
                {
                    ++numNonSeqCst;
                    Console.WriteLine($"StoreLoad re-ordering #{numNonSeqCst} detected after {i + 1} iterations: x0 = {x0}, x1 = {x1}");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"{numNonSeqCst} of {numIterations} were not sequentially consistent.");
            if(numNonSeqCst > 0)
            {
                Console.WriteLine($"That's about 1 in every " + numIterations / numNonSeqCst + " executions or " + 100 * numNonSeqCst / (float) numIterations + "%");
                if(useMemoryBarriers)
                {
                    Console.WriteLine("That seems wrong as you're using memory barriers.");
                }
                else
                {
                    Console.WriteLine("And that's OK, because you weren't using memory barriers.");
                }
            }
            else if(useMemoryBarriers)
            {
                Console.WriteLine("And that's to be expected, because you were using memory barriers.");
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoOptimization)]
        private static void FirstThreadFunction()
        {
            while(true)
            {
                first.WaitOne();
                x0 = 1;
                maybeMemoryBarrier();
                r0 = x1;
                barrier.SignalAndWait();
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoOptimization)]
        private static void SecondThreadFunction()
        {
            while(true)
            {
                second.WaitOne();
                x1 = 1;
                maybeMemoryBarrier();
                r1 = x0;
                barrier.SignalAndWait();
            }
        }
    }
}
