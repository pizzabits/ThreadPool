using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ThreadPool
{
    public delegate void WaitCallback(Object state);

    //private class 

    public static class ThreadPool
    {
        private static int _maxThreads = 1000;
        public static int FinishedThreads = 0;
        public static int TotalQueued = 0;
        public static int MaxThreadsUsed = 0;
        private static ConcurrentQueue<WaitCallback> _queue = new ConcurrentQueue<WaitCallback>();
        private static List<Thread> _workers = new List<Thread>(100);

        private static Thread _backgroundThread = new Thread(() =>
        {
            while (true)
            {
                DequeueRun();
                Thread.Sleep(20);
            }
        });

        public static Boolean QueueUserWorkItem(WaitCallback callBack)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException("callBack is null.");
            }

            //check for: NotSupportedException: The common language runtime (CLR) is hosted, and the host does not support this action. 

            lock (_queue)
            {
                _queue.Enqueue(callBack);
                TotalQueued++;
            }

            lock (_backgroundThread)
            {
                if (_backgroundThread.ThreadState == ThreadState.Unstarted)
                {
                    _backgroundThread.IsBackground = true;
                    _backgroundThread.Start();
                }
            }
            return true;
        }

        private static void DequeueRun()
        {
            WaitCallback wcb = null;
            Thread stoppedThread = null;

            lock (_workers)
            {
                if (FinishedThreads > 0) //unprotected access!!!
                { //there may be threads need to be renewed / removed
                    for (int i = 0; i < _workers.Count; i++)
                    {
                        if (_workers[i].ThreadState == ThreadState.Stopped)
                        {
                            stoppedThread = _workers[i]; //get a reference to the stopped thread
                        }
                    }
                }

                if (_workers.Count > MaxThreadsUsed) //reassign the maximum threads used
                {
                    MaxThreadsUsed = _workers.Count;
                }
                lock (_queue)
                {
                    if (_queue.Count > 0 && _maxThreads - _workers.Count > 0)
                    {
                        _queue.TryDequeue(out wcb); //if there's still work, dequeue something
                    }
                }
            } //release everything while preparing a new independant thread

            if (wcb == null && stoppedThread != null) //dequeued nothing and there exist a stopped thread
            {
                bool empty = false;
                lock (_queue) //find out if the queue is empty
                {
                    empty = _queue.Count == 0;
                }
                if (empty)
                {
                    lock (_workers)
                    {
                        if (!_workers.Remove(stoppedThread))
                        {
                            throw new SystemException("Could not remove thread from the list!");
                        }
                        else
                        {
                            Interlocked.Decrement(ref FinishedThreads);
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
                        lock (_queue) //let other active threads do the work.
                        {
                            _queue.Enqueue(wcb);
                            wcb = null;
                        }
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
                    lock (_queue)
                    {
                        if (_queue.Count > 0) //help a non-empty queue to get rid of its load
                        {
                            _queue.TryDequeue(out wcb);
                        }
                    }
                }

                if (wcb != null)
                {
                    wcb.Invoke(null);
                }
                else
                { //could not dequeue from the queue, terminate the thread
                    Interlocked.Increment(ref FinishedThreads);
                    return;
                }

                Thread.Sleep(20); //sleep enough time to switch between threads
            }
        }
    }
}