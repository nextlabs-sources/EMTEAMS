# Readme
*author: tiger hao*

- Microsoft docs indicate the Communication SDK don't include in Graph API SDK v1.0, therefore, Graph API SDK v1.0 has supported communication function. If the graph beta SDK is needed later, reference [here](https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet), add the alias for beta version.

- appsetting.json

  ```json
  {
    //Because of Bot Framework's limitation, below four attributes should be put root {}
    "MicrosoftAppId": "c25d46ca-e3de-4575-8eec-6f6a355d8c00", //Bot Id
    "MicrosoftAppPassword": "43dKNCc?ZG@Swc:IVvRGW@oGTasMLI31",	//Bot Key
    "PlaceCallEndpointUrl": "https://graph.microsoft.com/beta",	//Graph entry point.
    "BotBaseUrl": "https://937fab67.ngrok.io",	//Website Url
  
    "AzureAd": {	//Azure auth and Graph app
      "Instance": "https://login.microsoftonline.com/",	//Anth entry point
      "TenantId": "59bdaafc-4fd9-41b3-9cd3-539015dae094",	//Tenant ID
      "AppId": "a445b4d5-7111-4f28-9587-a51a5476aa4d",	//App ID
      "AppSecret": "=Fl-J:YzKmjsvdop=C6z64GZ5T6raGLE"	//App Key
    },
  
    "Logging": {	//Logging configuration
      "IncludeScopes": false,
      "LogLevel": {
        "Default": "Information",
        "System": "Information",
        "Microsoft": "Information"
      }
    },
  
    "Extension": {	//TO DO, add more logging output methods
      "DebugMode": "dbgView"
    }
  } 
  ```

- log4net.config

  ```xml
  <?xml version="1.0" encoding="utf-8" ?>
  <log4net>
    <appender name="ErrorRollingFileAppender" type="log4net.Appender.RollingFileAppender">
      <file value="C://log//" />
      <appendToFile value="true" />
      <rollingStyle value="Date"/>
      <datePattern value="yyyy-MM-dd-'error.log'"/>
      <maxSizeRollBackups value="100" />
      <staticLogFileName value="false" />
      <encoding value="utf-8" />
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%newline%date [%thread %-5level] %n -- %m%n" />
      </layout>
      <filter type="log4net.Filter.LevelRangeFilter">
        <levelMin value="ERROR" />
        <levelMax value="FATAL" />
      </filter>
    </appender>
  
    <appender name="WarnRollingFileAppender" type="log4net.Appender.RollingFileAppender">
      <file value="C://log//" />
      <appendToFile value="true" />
      <rollingStyle value="Date"/>
      <datePattern value="yyyy-MM-dd-'warn.log'"/>
      <maxSizeRollBackups value="100" />
      <staticLogFileName value="false" />
      <encoding value="utf-8" />
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%newline%date [%thread %-5level] %n -- %m%n" />
      </layout>
      <filter type="log4net.Filter.LevelRangeFilter">
        <levelMin value="WARN" />
        <levelMax value="WARN" />
      </filter>
    </appender>
  
    <appender name="InfoRollingFileAppender" type="log4net.Appender.RollingFileAppender">
      <file value="C://log//" />
      <appendToFile value="true" />
      <rollingStyle value="Date"/>
      <datePattern value="yyyy-MM-dd-'info.log'"/>
      <maxSizeRollBackups value="100" />
      <staticLogFileName value="false" />
      <encoding value="utf-8" />
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%newline%date [%thread %-5level] %n -- %m%n" />
      </layout>
      <filter type="log4net.Filter.LevelRangeFilter">
        <levelMin value="TRACE " />
        <levelMax value="INFO" />
      </filter>
    </appender>
  
    <root>
      <level value="All" />
      <appender-ref ref="ErrorRollingFileAppender" />
      <appender-ref ref="WarnRollingFileAppender" />
      <appender-ref ref="InfoRollingFileAppender" />
    </root>
  </log4net>
  ```

  