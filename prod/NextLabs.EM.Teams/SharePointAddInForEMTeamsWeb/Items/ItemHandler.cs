using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SharePointAddInForEMTeamsWeb
{
	public static class ItemHandler
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static BlockingCollection<ItemPackage> itemsBQ = new BlockingCollection<ItemPackage>();

		//running status flag
		private static readonly object runningLock = new object();
		private static bool runningStatus = false;
		private const int maxRunning = 7;

		public static void Push(ItemPackage package)
		{
			itemsBQ.Add(package);
		}

		public static void Consume()
		{
			foreach (var package in itemsBQ.GetConsumingEnumerable()) 
			{
				try
				{
					package.ProcessFileAsync();
				}
				catch (Exception e)
				{
					logger.Error($"Consume Error: {e}");
				}
			}
		}

		public static void Run() 
		{
			lock (runningLock)
			{
				if (!runningStatus)
				{
					try
					{
						for (int i = 0; i < maxRunning; ++i)
						{
							Task.Factory.StartNew(new Action(Consume));
						}
						runningStatus = true;
						logger.Info($"Consume Task Started.");
					}
					catch (Exception e)
					{
						logger.Error($"PushAndRun Error: {e}");
					}
				}
			}
		}

		public static void PushAndRun(ItemPackage package)
		{
			Push(package);
			if (!runningStatus) Run();
		}
	}
}