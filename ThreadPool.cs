using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ThreadPool
{
    public delegate void WaitCallback(Object state);

    public static class ThreadPool
    {
        private static int _maxThreads = 1000; // an assumption.
        private static int _finishedThreads = 0;
        private static int _totalQueued = 0;
        private static int _maxThreadsUsed = 0;
        private static ConcurrentQueue<WaitCallback> _queue = new ConcurrentQueue<WaitCallback>();
        private static List<Thread> _workers = new List<Thread>(100);
        private static Timer _timer = new Timer(new TimerCallback((state) =>
        {
            DequeueRun();
        }), null, 50, 50);

        public static int GetMaxThreadsUsed()
        {
            return _maxThreadsUsed;
        }

        public static int GetTotalQueuedJobs()
        {
            return _totalQueued;
        }

        public static int GetNumberOfFinishedThreads()
        {
            return _finishedThreads;
        }
        public static Boolean QueueUserWorkItem(WaitCallback callBack)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException("callBack is null.");
            }

            //check for: NotSupportedException: The common language runtime (CLR) is hosted, and the host does not support this action. 

            _queue.Enqueue(callBack);
            Interlocked.Increment(ref _totalQueued);
 
            return true;
        }

        private static void DequeueRun()
        {
            WaitCallback wcb = null;
            Thread stoppedThread = null;

            lock (_workers)
            {
                if (_finishedThreads > 0) //not an atomic read
                { //there may be threads need to be renewed / removed
                    for (int i = 0; i < _workers.Count; i++)
                    {
                        if (_workers[i].ThreadState == ThreadState.Stopped)
                        {
                            stoppedThread = _workers[i]; //get a reference to the stopped thread
                        }
                    }
                }

                if (_workers.Count > _maxThreadsUsed) //reassign the maximum threads used
                {
                    _maxThreadsUsed = _workers.Count;
                }

                if (_queue.Count > 0 && _maxThreads - _workers.Count > 0)
                {
                    _queue.TryDequeue(out wcb); //if there's still work, dequeue something
                }
                
            } //release everything while preparing a new independant thread

            if (wcb == null && stoppedThread != null) //dequeued nothing and there exist a stopped thread
            {
                if (_queue.IsEmpty)
                {
                    lock (_workers)
                    {
                        if (!_workers.Remove(stoppedThread))
                        {
                            throw new SystemException("Could not remove a thread from the list!");
                        }
                        else
                        {
                            Interlocked.Decrement(ref _finishedThreads);
                        }
                    }
                }
            }
            else if (wcb != null)
            {
                Thread thread = new Thread(new ParameterizedThreadStart(ThreadFunc));
                /* The threads in the managed thread pool are background threads.
                 * That is, their IsBackground properties are true. 
                 * This means that a ThreadPool thread will not keep an application running after all foreground threads have exited. */
                thread.IsBackground = true;
                lock (_workers)
                {
                    if (_maxThreads - _workers.Count > 0)
                    {
                        if (stoppedThread != null) //replace the stopped thread with a new one
                        {
                            _workers[_workers.IndexOf(stoppedThread)] = thread; 
                        }
                        else
                        {
                            _workers.Add(thread);
                        }
                    }
                    else
                    {
                        _queue.Enqueue(wcb);
                        wcb = null;
                    }
                }
                if (wcb != null)
                {
                    thread.Start(wcb);
                }
            }
        }

        public static void ThreadFunc(Object obj)
        {
            //when a thread is started, it already has a job assigned to it.
            WaitCallback wcb = obj as WaitCallback;
            if (wcb == null)
            {
                throw new ArgumentException("ThreadFunc must recieve WaitCallback as a parameter!");
            }
            wcb.Invoke(null);

            while (true) //from now on, I'm dequeueing/invoking jobs from the queue.
            {
                wcb = null;

                lock (_workers)
                {
                    if (!_queue.IsEmpty) //help a non-empty queue to get rid of its load
                    {
                        _queue.TryDequeue(out wcb);
                    }
                }

                if (wcb != null)
                {
                    wcb.Invoke(null);
                }
                else
                { //could not dequeue from the queue, terminate the thread
                    Interlocked.Increment(ref _finishedThreads);
                    return;
                }

                Thread.Sleep(0); //context switch
            }
        }
    }
}