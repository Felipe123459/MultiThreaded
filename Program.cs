using System;
using System.Threading;

namespace ThreadingProject
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Missing argument. Usage: ThreadingProject.exe [Phase1-4]");
                return;
            }
            //Switch statement to let the user pick which phase to execute
            switch (args[0].ToLower()) 
            {
                case "phase1":
                    RunBasicThreads();
                    break;
                case "phase2":
                    RunThreadSafeBanking();
                    break;
                case "phase3":
                    SimulateDeadlock();
                    break;
                case "phase4":
                    FixDeadlock();
                    break;
                default:
                    Console.WriteLine("Invalid phase. Choose from Phase1, Phase2, Phase3, or Phase4.");
                    break;
            }
        }

        //Basic threading 
        static void RunBasicThreads()
        {
            var account = new SimpleAccount(1000);
            var threads = new Thread[4];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    account.Withdraw(100);
                    account.Deposit(50);
                });
                threads[i].Start();
            }

            foreach (var thread in threads) thread.Join();

            Console.WriteLine($"Final balance (Phase1): {account.Balance}");
        }

        //Mutex-protected banking 
        static void RunThreadSafeBanking()
        {
            var account = new SecureAccount(500);
            var threads = new Thread[4];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    account.Withdraw(100);
                    account.Deposit(50);
                });
                threads[i].Start();
            } 

            foreach (var thread in threads) thread.Join();

            Console.WriteLine($"Final balance (Phase2): {account.GetBalance()}");
        }

        //deadlock banking 
        static void SimulateDeadlock()
        {
            var acc1 = new DeadlockAccount(1, 1000);
            var acc2 = new DeadlockAccount(2, 1000);

            var t1 = new Thread(() => acc1.Transfer(acc2, 100));
            var t2 = new Thread(() => acc2.Transfer(acc1, 100));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            
        }

        //DeadLock free banking 
        static void FixDeadlock()
        {
            var acc1 = new SmartAccount(1, 1000);
            var acc2 = new SmartAccount(2, 500);

            var t1 = new Thread(() => acc1.Transfer(acc2, 100));
            var t2 = new Thread(() => acc2.Transfer(acc1, 100));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Console.WriteLine($"Final balances: Acc1 = {acc1.Balance}, Acc2 = {acc2.Balance}");
        }
    }

    //Phase 1
    class SimpleAccount
    {
        private decimal balance;
        private readonly object LObj = new object();//Create a lock object

        public SimpleAccount(decimal initialBalance) => balance = initialBalance;

        public void Deposit(decimal amount)
        {
            lock (LObj)
            {
                balance += amount;
            }
        }

        public void Withdraw(decimal amount)
        {
            lock (LObj)
            {
                balance -= amount;
            }
        }

        public decimal Balance
        {
            get { lock (LObj) { return balance; } }
        }
    }//Create a basic bank account with no thread safety

    //Phase 2
    class SecureAccount
    {
        private decimal balance;
        private readonly Mutex mutex = new Mutex();

        public SecureAccount(decimal initialBalance) => balance = initialBalance;

        public void Deposit(decimal amount)
        {
            mutex.WaitOne();
            try { balance += amount; }
            finally { mutex.ReleaseMutex(); }
        }

        public void Withdraw(decimal amount)
        {
            mutex.WaitOne();
            try { balance -= amount; }
            finally { mutex.ReleaseMutex(); }
        }

        public decimal GetBalance()
        {
            mutex.WaitOne();
            try { return balance; }
            finally { mutex.ReleaseMutex(); }
        }//Ensures only account is being accessed
    }

    //Phase 3
    class DeadlockAccount
    {
        public int Id { get; }
        public decimal Balance { get; private set; }
        private readonly Mutex mutex = new Mutex();

        public DeadlockAccount(int id, decimal balance)
        {
            Id = id;
            Balance = balance;
        }

        public void Transfer(DeadlockAccount target, decimal amount)
        {
             Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} locking Account {Id}");

        if (!mutex.WaitOne(TimeSpan.FromSeconds(1))) //Try acquiring lock with timeout
        {
        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} failed to lock Account {Id}, possible deadlock!");
        return;
        }

        Thread.Sleep(100); // Simulate processing delay

        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} locking Account {target.Id}");

        if (!target.mutex.WaitOne(TimeSpan.FromSeconds(1)))//Try acquiring second lock with timeout
        {
        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} failed to lock Account {target.Id}, possible deadlock!");
        mutex.ReleaseMutex(); //Release first lock to avoid holding resources indefinitely
        return;
        }
        }

    


    }

    //Phase 4
    class SmartAccount
    {
        public int Id { get; }
        public decimal Balance { get; private set; }
        private readonly object LObj = new object();//Create a lock object

        public SmartAccount(int id, decimal balance)
        {
            Id = id;
            Balance = balance;
        }

        public void Transfer(SmartAccount target, decimal amount)
        {
            int maxAttempts = 3;
            bool transferSuccessful = false;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Console.WriteLine($"Attempt {attempt}: Transferring from {Id} to {target.Id}");

                var first = Id < target.Id ? this : target;
                var second = Id < target.Id ? target : this;//Locks the accounts

                bool firstLock = false, secondLock = false;

                try
                {
                    firstLock = Monitor.TryEnter(first.LObj, 1500);
                    if (!firstLock) continue;

                    secondLock = Monitor.TryEnter(second.LObj, 1500);
                    if (!secondLock) continue;

                    Balance -= amount;
                    target.Balance += amount;
                    transferSuccessful = true;
                    Console.WriteLine($"Transfer completed: {Id} → {target.Id}");
                    break;//Transfer if key was acquired
                }
                finally
                {
                    if (secondLock) Monitor.Exit(second.LObj);
                    if (firstLock) Monitor.Exit(first.LObj);
                }

                Thread.Sleep(new Random().Next(100, 500));//Random delay to avoid livelock
            }

            if (!transferSuccessful)
            {
                Console.WriteLine($"Transfer {Id} → {target.Id} failed after {maxAttempts} attempts.");
            }
        }
    }
}