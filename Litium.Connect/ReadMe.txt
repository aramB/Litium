In this sample we will send Order infomation to ErpConnector. The connector will log a message into log file to display the received orderId. 


1. Send confirmed order to ErpConnector using Accelerator Package 7.2.2 :  
 - In litium Accelerator package, add reference to Litium.Connect.Erp.Abstractions and Litium.Connect.Erp.Application project for Litium.Accelerator and Litium.Accelerator.Mvc project.
 - In OrderStateBuilder.cs, Method Build, under order confirmed logic (line 53), add these line : 

          //Register order with ERP
          IoC.Resolve<ConnectErpApi>().SendOrder(orderCarrier.ID);

2. Host LitiumSample.ErpConnector in a sample website :
 - Create a sample website template in visual Studio
 - Add reference to LitiumSample.ErpConnector project
 - Hosting this sample website on IIS , make sure we could access to OrderController (sample url : http://erpconnector.local/api/order)
3. Config Erpconnector endpoint in Litium.Connect.Erp.Application
 - Open app.config in Litium.Connect.Erp.Application, update endPoint with the location that we host OrderController in step #2

Sample Config

<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <configSections>
    <section name ="pluginSettings" type="Litium.Connect.Erp.Application.Configuration.PluginSettingsConfigSection, Litium.Connect.Erp.Application"/>
  </configSections>
  <pluginSettings traceMode="true">
    <addOnEndPoints>
      <add endPoint="http://erpconnector.local/api/order" connectHubType="Erp" httpMethods="GET,POST" />
    </addOnEndPoints>
  </pluginSettings>
</configuration>

4. Create an order in Accelerator, expect to see in litium.log the response message from ErpConnector as below :

[TRACE] [] Litium.Connect.Erp.Abstractions.MessageQueue.IMessageProcessor - Response message : "Order cacaf688-15a0-48c8-bcc1-1379d19c2901 has been registered successfully." 