// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Logging
{
    using Microsoft.Extensions.Logging;

    public static class Log4netExtensions
    {
        public static ILoggerFactory AddLog4Net(this ILoggerFactory factory, string log4NetConfigFile = "log4net.config")
        {
            factory.AddProvider(new Log4NetProvider(log4NetConfigFile));
            return factory;
        }
    }
}
