using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ParseHelper
{

    public class ThreadManager : IEnumerable<Thread>
    {
        private readonly List<Thread> _mainThreads = new List<Thread>();
        public int AllowSessionsCount { get; }
        public int ActiveSessionsCount { get; private set; }

        private readonly object _synchronizationPlug = new object();

        
        public delegate void ThreadsEnded();
        public event ThreadsEnded ThreadsEndedEvent;


        public ThreadManager(int allowSessionsCount) => AllowSessionsCount = allowSessionsCount;
        public ThreadManager()
        {
            AllowSessionsCount = Environment.ProcessorCount / 2;
            StartDebugger();
        }

        /// 
        ///DEBUG
        /// 
        public static bool UseDebugger = false;
        void StartDebugger()
        {
            new Thread(() =>
                {
                    int oldval = 0;
                    while (!UseDebugger)
                    {
                        var r = ActiveSessionsCount;
                        if (r != oldval)
                        {
                            Console.WriteLine(
                                $"active sessions: {ActiveSessionsCount};\nmax sessions: {AllowSessionsCount}; \nwaiting sessions: {_mainThreads.Count}.\n");
                        }

                        oldval = r;

                    }
                }
            ).Start();
        }
        ///
        
        public void Add(Thread th)
        {
            Thread sourceThreadHolder = new Thread(() =>
            {
                th.Start();
                th.Join();

                lock (_synchronizationPlug)
                {
                    ActiveSessionsCount--;

                    while (ActiveSessionsCount < Math.Min(AllowSessionsCount, ActiveSessionsCount + _mainThreads.Count))
                    {
                        ActiveSessionsCount++;
                        var firstUnstatedThread = _mainThreads.FirstOrDefault();

                        if (firstUnstatedThread != null)
                        {
                            _mainThreads.Remove(firstUnstatedThread);
                            firstUnstatedThread.Start();
                        }
                    }

                    if (_mainThreads.Count == 0 && ActiveSessionsCount == 0)
                        ThreadsEndedEvent?.Invoke();
                }
            });

            lock (_synchronizationPlug)
            {
                _mainThreads.Add(sourceThreadHolder);

                while (ActiveSessionsCount < Math.Min(AllowSessionsCount, ActiveSessionsCount + _mainThreads.Count))
                {
                    ActiveSessionsCount++;
                    var firstUnstatedThread = _mainThreads.FirstOrDefault();

                    if (firstUnstatedThread != null)
                    {
                        _mainThreads.Remove(firstUnstatedThread);
                        firstUnstatedThread.Start();
                    }
                }
            }
        }

        public delegate void WaiterDelegate();

        public void Wait(WaiterDelegate waiterFunc)
        {
            bool isFinished = false;
            void FinishMarker()
            {
                isFinished = true;
            }
            ThreadsEndedEvent += FinishMarker;

            waiterFunc?.Invoke();

            while (!isFinished) { }
            ThreadsEndedEvent -= FinishMarker;
        }

        public void DoAfter(WaiterDelegate asyncFunc, WaiterDelegate belatedFunc)
        {
            new Thread(() =>
            {
                Wait(asyncFunc);
                belatedFunc?.Invoke();
            })
                .Start();
        }

        public Thread this[int i] => _mainThreads[i];
        public IEnumerator<Thread> GetEnumerator() => _mainThreads.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _mainThreads.GetEnumerator();
    }


}