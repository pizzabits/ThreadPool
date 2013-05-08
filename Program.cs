using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPool
{
    public sealed class Program
    {
        public static int NumberOfThreadsWrote = 0;

        public static void Main(string[] args)
        {
            // Queue some tasks.
            for (int i = 0; i < 5000; i++)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc));
            }

            Console.WriteLine("Main thread does some work, then sleeps.");
            // If you comment out the Sleep, the main thread exits before 
            // the thread pool task runs.  The thread pool uses background 
            // threads, which do not keep the application running.  (This 
            // is a simple example of a race condition.)
            Thread.Sleep(1 * 4 * 1000);

            Console.WriteLine("Completed {0} tasks using a maximum concurrency of {1} threads.", NumberOfThreadsWrote, ThreadPool.GetMaxThreadsUsed());
            Console.WriteLine("Total items queued: {0} and now there are {1} threads in the inner list of the thread pool.", ThreadPool.GetTotalQueuedJobs(), ThreadPool.GetNumberOfFinishedThreads());
            Console.WriteLine("Main thread exits.");
        }

        // This thread procedure performs the task. 
        public static void ThreadProc(Object stateInfo)
        {
            // No state object was passed to QueueUserWorkItem, so  
            // stateInfo is null.
            int number = Interlocked.Increment(ref NumberOfThreadsWrote);
            Console.WriteLine("Hello from the thread pool. Done {0} tasks.", number);
        }
    }
}