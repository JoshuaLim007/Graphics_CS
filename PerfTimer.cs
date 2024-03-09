using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    internal static class PerfTimer
    {
        class Watcher
        {
            public string name { get; } =  "";
            public Watcher(string name)
            {
                this.name = name;
            }
            public Stopwatch stopwatch = new Stopwatch();
            public double totalMs = 0;
            public int samples = 0;
            public int calls = 0;
            public bool sampleflag = true;
            public void Reset()
            {
                samples = 0;
                totalMs = 0;
                calls = 0;
                sampleflag = true;
                stopwatch.Stop();
                stopwatch.Reset();
            }
        }

        static public uint Intervals 
        {
            get => interval;
            set
            {
                if(value == 0)
                {
                    value = 1;
                }
                interval = value;
            }
        }
        static uint interval = 500;
        static uint timer = 0;
        static Dictionary<string, Watcher> timerDic = new Dictionary<string, Watcher>();
        static List<Watcher> watchers = new List<Watcher>();
        static Stack<Watcher> startedWatcher = new Stack<Watcher>();
        public static void Start(string name)
        {
            if (timerDic.TryGetValue(name, out var watch))
            {
                if (watch.stopwatch.IsRunning)
                {
                    throw new Exception("PerfTimer for: " + name + " is already running");
                }
                watch.stopwatch.Start();
                startedWatcher.Push(watch);
            }
            else
            {
                var watcher = new Watcher(name);
                watcher.stopwatch.Start();
                watchers.Add(watcher);
                timerDic.Add(name, watcher);
                startedWatcher.Push(watcher);
            }
        }
        public static void Stop()
        {
            var startedWatcher = PerfTimer.startedWatcher.Pop();
            startedWatcher.stopwatch.Stop();
            startedWatcher.totalMs += startedWatcher.stopwatch.Elapsed.TotalMilliseconds;
            startedWatcher.stopwatch.Reset();
            startedWatcher.calls++;

            if (startedWatcher.sampleflag == true)
            {
                startedWatcher.samples++;
                startedWatcher.sampleflag = false;
            }
        }

        public static void ResetTimers(bool printResults)
        {
            timer++;
            bool skip = true;
            if (timer >= Intervals)
            {
                timer = 0;
                skip = false;
                Console.WriteLine("--------------------------");
            }
            for (int i = 0; i < watchers.Count; i++)
            {
                watchers[i].sampleflag = true;
                if (skip)
                {
                    continue;
                }

                if (watchers[i].samples == 0)
                    continue;

                if (printResults)
                {
                    float time = (float)(watchers[i].totalMs / watchers[i].samples);
                    float calls = (float)(watchers[i].calls / watchers[i].samples);
                    Console.WriteLine(calls + " " + watchers[i].name + ": " + time + "ms");
                }
                watchers[i].Reset();
            }
        }
        public static void Clear()
        {
            timerDic.Clear();
            watchers.Clear();
            startedWatcher.Clear();
        }
    }
}
